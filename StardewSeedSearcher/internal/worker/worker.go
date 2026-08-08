package worker

import (
	"bufio"
	"context"
	"encoding/json"
	"errors"
	"fmt"
	"io"
	"log"
	"os"
	"os/exec"
	"path/filepath"
	"sync"
	"sync/atomic"
)

type Request struct {
	ID         string          `json:"id,omitempty"`
	Type       string          `json:"type"`
	Request    json.RawMessage `json:"request,omitempty"`
	StartSeed  int64           `json:"startSeed,omitempty"`
	EndSeed    int64           `json:"endSeed,omitempty"`
	MaxResults int             `json:"maxResults,omitempty"`
}

type Response struct {
	ID           string        `json:"id,omitempty"`
	Type         string        `json:"type"`
	CheckedCount int64         `json:"checkedCount,omitempty"`
	Matches      []SeedMatch   `json:"matches,omitempty"`
	FeatureStats []FeatureStat `json:"featureStats,omitempty"`
	Items        []string      `json:"items,omitempty"`
	Error        string        `json:"error,omitempty"`
}

type SeedMatch struct {
	Seed            int             `json:"seed"`
	Details         json.RawMessage `json:"details,omitempty"`
	EnabledFeatures json.RawMessage `json:"enabledFeatures,omitempty"`
}

type FeatureStat struct {
	Name      string `json:"name"`
	PassCount int64  `json:"passCount"`
}

type Pool struct {
	clients []*Client
	next    atomic.Uint64
}

// NewPool starts a pool of long-lived C# worker clients.
func NewPool(ctx context.Context, size int) (*Pool, error) {
	if size < 1 {
		size = 1
	}

	clients := make([]*Client, 0, size)
	for i := 0; i < size; i++ {
		client, err := Start(ctx, i)
		if err != nil {
			for _, existing := range clients {
				existing.Close()
			}
			return nil, err
		}
		clients = append(clients, client)
	}

	return &Pool{clients: clients}, nil
}

// Len returns the number of workers in the pool.
func (p *Pool) Len() int {
	return len(p.clients)
}

// Call sends a request to the next worker in round-robin order.
func (p *Pool) Call(req Request) (Response, error) {
	if len(p.clients) == 0 {
		return Response{}, errors.New("worker pool is empty")
	}
	index := int(p.next.Add(1)-1) % len(p.clients)
	return p.CallAt(index, req)
}

// CallAt sends a request to the worker at the given index.
func (p *Pool) CallAt(index int, req Request) (Response, error) {
	if len(p.clients) == 0 {
		return Response{}, errors.New("worker pool is empty")
	}
	index %= len(p.clients)
	if index < 0 {
		index += len(p.clients)
	}
	return p.clients[index].Call(req)
}

// Close stops all worker clients in the pool.
func (p *Pool) Close() {
	for _, client := range p.clients {
		client.Close()
	}
}

type Client struct {
	id     int
	cmd    *exec.Cmd
	stdin  io.WriteCloser
	stdout *bufio.Reader
	mu     sync.Mutex
	seq    atomic.Uint64
}

// Start launches one C# worker process and wraps it as a client.
func Start(ctx context.Context, id int) (*Client, error) {
	name, args, workDir, err := resolveWorkerCommand()
	if err != nil {
		return nil, err
	}

	cmd := exec.CommandContext(ctx, name, args...)
	cmd.Dir = workDir
	cmd.Env = append(os.Environ(), "DOTNET_CLI_TELEMETRY_OPTOUT=1")

	stdin, err := cmd.StdinPipe()
	if err != nil {
		return nil, err
	}
	stdoutPipe, err := cmd.StdoutPipe()
	if err != nil {
		return nil, err
	}
	stderrPipe, err := cmd.StderrPipe()
	if err != nil {
		return nil, err
	}

	if err := cmd.Start(); err != nil {
		return nil, err
	}

	client := &Client{
		id:     id,
		cmd:    cmd,
		stdin:  stdin,
		stdout: bufio.NewReader(stdoutPipe),
	}

	go logWorkerStderr(id, stderrPipe)
	go func() {
		if err := cmd.Wait(); err != nil && ctx.Err() == nil {
			log.Printf("C# worker %d exited: %v", id, err)
		}
	}()

	return client, nil
}

// Call sends one JSON-line request and waits for one JSON-line response.
func (c *Client) Call(req Request) (Response, error) {
	c.mu.Lock()
	defer c.mu.Unlock()

	if req.ID == "" {
		req.ID = fmt.Sprintf("w%d-%d", c.id, c.seq.Add(1))
	}

	payload, err := json.Marshal(req)
	if err != nil {
		return Response{}, err
	}

	if _, err := c.stdin.Write(append(payload, '\n')); err != nil {
		return Response{}, err
	}

	line, err := c.stdout.ReadBytes('\n')
	if err != nil {
		return Response{}, err
	}

	var resp Response
	if err := json.Unmarshal(line, &resp); err != nil {
		return Response{}, err
	}
	if resp.Error != "" {
		return resp, errors.New(resp.Error)
	}

	return resp, nil
}

// Close terminates the underlying C# worker process.
func (c *Client) Close() {
	_ = c.stdin.Close()
	if c.cmd != nil && c.cmd.Process != nil {
		_ = c.cmd.Process.Kill()
	}
}

// resolveWorkerCommand locates or builds the C# worker entry point.
func resolveWorkerCommand() (string, []string, string, error) {
	cwd, err := os.Getwd()
	if err != nil {
		return "", nil, "", err
	}

	projectDirs := []string{cwd, filepath.Join(cwd, "StardewSeedSearcher")}
	for _, dir := range projectDirs {
		project := filepath.Join(dir, "StardewSeedSearcher.csproj")
		if _, err := os.Stat(project); err != nil {
			continue
		}

		releaseDll := filepath.Join(dir, "bin", "Release", "net9.0", "StardewSeedSearcher.dll")
		if _, err := os.Stat(releaseDll); err == nil {
			return "dotnet", []string{releaseDll, "--worker"}, dir, nil
		}

		debugDll := filepath.Join(dir, "bin", "Debug", "net9.0", "StardewSeedSearcher.dll")
		if _, err := os.Stat(debugDll); err == nil {
			return "dotnet", []string{debugDll, "--worker"}, dir, nil
		}

		log.Printf("C# worker binary was not found; running dotnet build for %s", project)
		build := exec.Command("dotnet", "build", project)
		build.Dir = dir
		build.Env = append(os.Environ(), "DOTNET_CLI_TELEMETRY_OPTOUT=1")
		if output, err := build.CombinedOutput(); err != nil {
			return "", nil, "", fmt.Errorf("dotnet build failed: %w\n%s", err, string(output))
		}
		if _, err := os.Stat(debugDll); err == nil {
			return "dotnet", []string{debugDll, "--worker"}, dir, nil
		}
		return "", nil, "", fmt.Errorf("dotnet build completed, but %s was not created", debugDll)
	}

	return "", nil, "", fmt.Errorf("StardewSeedSearcher.csproj was not found under %s", cwd)
}

// logWorkerStderr forwards worker stderr lines to the Go logger.
func logWorkerStderr(id int, reader io.Reader) {
	scanner := bufio.NewScanner(reader)
	for scanner.Scan() {
		log.Printf("C# worker %d: %s", id, scanner.Text())
	}
}
