package server

import (
	"context"
	"encoding/json"
	"errors"
	"fmt"
	"log"
	"math"
	"net/http"
	"os"
	"strconv"
	"sync"
	"sync/atomic"
	"time"

	"stardewseedsearcher/internal/worker"
	"stardewseedsearcher/internal/ws"
)

type Server struct {
	pool *worker.Pool
	hub  *ws.Hub

	mu     sync.Mutex
	active *searchRun
}

type searchRun struct {
	cancel    context.CancelFunc
	cancelled atomic.Bool
}

// New creates a server with the given C# worker pool.
func New(pool *worker.Pool) *Server {
	return &Server{
		pool: pool,
		hub:  ws.NewHub(),
	}
}

// ListenAndServe starts the HTTP server and shuts it down with the context.
func (s *Server) ListenAndServe(ctx context.Context, addr string) error {
	mux := http.NewServeMux()
	mux.HandleFunc("/", s.withCORS(s.handleRoot))
	mux.HandleFunc("/ws", s.hub.ServeHTTP)
	mux.HandleFunc("/api/health", s.withCORS(s.handleHealth))
	mux.HandleFunc("/api/seasons", s.withCORS(s.handleSeasons))
	mux.HandleFunc("/api/cart-items", s.withCORS(s.handleCartItems))
	mux.HandleFunc("/api/search", s.withCORS(s.handleSearch))
	mux.HandleFunc("/api/stop", s.withCORS(s.handleStop))

	server := &http.Server{Addr: addr, Handler: mux}
	errc := make(chan error, 1)
	go func() {
		errc <- server.ListenAndServe()
	}()

	select {
	case <-ctx.Done():
		shutdownCtx, cancel := context.WithTimeout(context.Background(), 3*time.Second)
		defer cancel()
		_ = server.Shutdown(shutdownCtx)
		return nil
	case err := <-errc:
		if errors.Is(err, http.ErrServerClosed) {
			return nil
		}
		return err
	}
}

// withCORS wraps a handler with permissive local-development CORS headers.
func (s *Server) withCORS(next http.HandlerFunc) http.HandlerFunc {
	return func(w http.ResponseWriter, r *http.Request) {
		w.Header().Set("Access-Control-Allow-Origin", "*")
		w.Header().Set("Access-Control-Allow-Methods", "GET, POST, OPTIONS")
		w.Header().Set("Access-Control-Allow-Headers", "Content-Type")
		if r.Method == http.MethodOptions {
			w.WriteHeader(http.StatusNoContent)
			return
		}
		next(w, r)
	}
}

// handleRoot returns a small status page for the hybrid backend.
func (s *Server) handleRoot(w http.ResponseWriter, r *http.Request) {
	if r.URL.Path != "/" {
		http.NotFound(w, r)
		return
	}
	w.Header().Set("Content-Type", "text/html; charset=utf-8")
	_, _ = w.Write([]byte("<!doctype html><meta charset=\"utf-8\"><title>StardewSeedSearcher</title><p>Go+C# hybrid backend is running.</p>"))
}

// handleHealth reports basic backend health and worker count.
func (s *Server) handleHealth(w http.ResponseWriter, r *http.Request) {
	writeJSON(w, http.StatusOK, map[string]any{
		"status":  "ok",
		"version": "go-csharp-hybrid",
		"workers": s.pool.Len(),
	})
}

// handleSeasons returns the fixed season list used by the frontend.
func (s *Server) handleSeasons(w http.ResponseWriter, r *http.Request) {
	writeJSON(w, http.StatusOK, []map[string]any{
		{"id": 0, "name": "\u6625"},
		{"id": 1, "name": "\u590f"},
		{"id": 2, "name": "\u79cb"},
		{"id": 3, "name": "\u51ac"},
	})
}

// handleCartItems asks C# for the traveling cart item list.
func (s *Server) handleCartItems(w http.ResponseWriter, r *http.Request) {
	if r.Method != http.MethodGet {
		http.Error(w, "method not allowed", http.StatusMethodNotAllowed)
		return
	}

	resp, err := s.pool.CallAt(0, worker.Request{Type: "cartItems"})
	if err != nil {
		http.Error(w, err.Error(), http.StatusInternalServerError)
		return
	}
	writeJSON(w, http.StatusOK, resp.Items)
}

// handleSearch starts an asynchronous seed search run.
func (s *Server) handleSearch(w http.ResponseWriter, r *http.Request) {
	if r.Method != http.MethodPost {
		http.Error(w, "method not allowed", http.StatusMethodNotAllowed)
		return
	}

	var raw json.RawMessage
	if err := json.NewDecoder(r.Body).Decode(&raw); err != nil {
		http.Error(w, err.Error(), http.StatusBadRequest)
		return
	}

	var req searchRequest
	if err := json.Unmarshal(raw, &req); err != nil {
		http.Error(w, err.Error(), http.StatusBadRequest)
		return
	}
	if req.EndSeed < req.StartSeed {
		http.Error(w, "endSeed must be greater than or equal to startSeed", http.StatusBadRequest)
		return
	}
	if req.OutputLimit <= 0 {
		req.OutputLimit = 20
	}

	runCtx, cancel := context.WithCancel(context.Background())
	run := &searchRun{cancel: cancel}

	s.mu.Lock()
	if s.active != nil {
		s.active.cancelled.Store(true)
		s.active.cancel()
	}
	s.active = run
	s.mu.Unlock()

	go s.runSearch(runCtx, run, raw, req)
	writeJSON(w, http.StatusOK, map[string]string{"message": "Search started."})
}

// handleStop cancels the currently active search run.
func (s *Server) handleStop(w http.ResponseWriter, r *http.Request) {
	if r.Method != http.MethodPost {
		http.Error(w, "method not allowed", http.StatusMethodNotAllowed)
		return
	}

	s.mu.Lock()
	if s.active != nil {
		s.active.cancelled.Store(true)
		s.active.cancel()
	}
	s.mu.Unlock()

	writeJSON(w, http.StatusOK, map[string]string{"message": "Stop requested."})
}

// runSearch partitions the seed range and streams worker results to clients.
func (s *Server) runSearch(ctx context.Context, run *searchRun, raw json.RawMessage, req searchRequest) {
	started := time.Now()
	total := req.EndSeed - req.StartSeed + 1
	s.hub.Broadcast(map[string]any{"type": "start", "total": total})

	chunkSize := int64(10000)
	if rawChunk := os.Getenv("SEED_GO_CHUNK"); rawChunk != "" {
		if parsed, err := strconv.ParseInt(rawChunk, 10, 64); err == nil && parsed > 0 {
			chunkSize = parsed
		}
	}

	jobs := make(chan seedRange, s.pool.Len()*2)
	results := make(chan worker.Response, s.pool.Len()*2)
	errs := make(chan error, s.pool.Len())

	var checked atomic.Int64
	var found atomic.Int64
	var hitLimit atomic.Bool
	stats := newStatCounter()

	var wg sync.WaitGroup
	for i := 0; i < s.pool.Len(); i++ {
		wg.Add(1)
		go func(workerIndex int) {
			defer wg.Done()
			for job := range jobs {
				if ctx.Err() != nil || hitLimit.Load() {
					return
				}

				remaining := req.OutputLimit - int(found.Load())
				if remaining <= 0 {
					hitLimit.Store(true)
					run.cancel()
					return
				}

				resp, err := s.pool.CallAt(workerIndex, worker.Request{
					Type:       "search",
					Request:    raw,
					StartSeed:  job.start,
					EndSeed:    job.end,
					MaxResults: remaining,
				})
				if err != nil {
					select {
					case errs <- err:
					default:
					}
					run.cancel()
					return
				}

				select {
				case results <- resp:
				case <-ctx.Done():
					return
				}
			}
		}(i)
	}

	go func() {
		defer close(jobs)
		for start := req.StartSeed; start <= req.EndSeed; start += chunkSize {
			if ctx.Err() != nil || hitLimit.Load() {
				return
			}
			end := start + chunkSize - 1
			if end > req.EndSeed || end < start {
				end = req.EndSeed
			}
			select {
			case jobs <- seedRange{start: start, end: end}:
			case <-ctx.Done():
				return
			}
			if end == math.MaxInt32 {
				return
			}
		}
	}()

	go func() {
		wg.Wait()
		close(results)
	}()

	lastProgress := time.Time{}
	for resp := range results {
		currentChecked := checked.Add(resp.CheckedCount)
		stats.add(resp.FeatureStats)

		for _, match := range resp.Matches {
			if found.Load() >= int64(req.OutputLimit) {
				hitLimit.Store(true)
				run.cancel()
				break
			}

			found.Add(1)
			s.hub.Broadcast(map[string]any{
				"type":            "found",
				"seed":            match.Seed,
				"details":         rawJSON(match.Details),
				"enabledFeatures": rawJSON(match.EnabledFeatures),
			})
		}

		if found.Load() >= int64(req.OutputLimit) {
			hitLimit.Store(true)
			run.cancel()
		}

		if time.Since(lastProgress) >= 500*time.Millisecond || currentChecked >= total || hitLimit.Load() {
			s.broadcastProgress(currentChecked, total, started, stats.snapshot())
			lastProgress = time.Now()
		}
	}

	select {
	case err := <-errs:
		log.Printf("hybrid search failed: %v", err)
	default:
	}

	finalChecked := checked.Load()
	s.broadcastProgress(finalChecked, total, started, stats.snapshot())
	s.hub.Broadcast(map[string]any{
		"type":       "complete",
		"totalFound": found.Load(),
		"elapsed":    round1(time.Since(started).Seconds()),
		"cancelled":  run.cancelled.Load() && !hitLimit.Load(),
	})

	s.mu.Lock()
	if s.active == run {
		s.active = nil
	}
	s.mu.Unlock()
}

// broadcastProgress sends a throttled progress update to WebSocket clients.
func (s *Server) broadcastProgress(checked int64, total int64, started time.Time, stats []worker.FeatureStat) {
	elapsed := time.Since(started).Seconds()
	speed := float64(0)
	if elapsed > 0 {
		speed = float64(checked) / elapsed
	}
	progress := float64(0)
	if total > 0 {
		progress = float64(checked) / float64(total) * 100
	}
	if progress > 100 {
		progress = 100
	}

	s.hub.Broadcast(map[string]any{
		"type":         "progress",
		"checkedCount": checked,
		"total":        total,
		"progress":     math.Round(progress*100) / 100,
		"speed":        math.Round(speed),
		"elapsed":      round1(elapsed),
		"featureStats": stats,
	})
}

type searchRequest struct {
	StartSeed   int64 `json:"startSeed"`
	EndSeed     int64 `json:"endSeed"`
	OutputLimit int   `json:"outputLimit"`
}

type seedRange struct {
	start int64
	end   int64
}

type statCounter struct {
	mu     sync.Mutex
	counts map[string]int64
	order  []string
}

// newStatCounter creates an ordered pass-count accumulator.
func newStatCounter() *statCounter {
	return &statCounter{counts: make(map[string]int64)}
}

// add merges feature statistics while preserving first-seen order.
func (s *statCounter) add(stats []worker.FeatureStat) {
	s.mu.Lock()
	defer s.mu.Unlock()
	for _, stat := range stats {
		if _, ok := s.counts[stat.Name]; !ok {
			s.order = append(s.order, stat.Name)
		}
		s.counts[stat.Name] += stat.PassCount
	}
}

// snapshot returns the accumulated statistics in stable order.
func (s *statCounter) snapshot() []worker.FeatureStat {
	s.mu.Lock()
	defer s.mu.Unlock()

	result := make([]worker.FeatureStat, 0, len(s.order))
	for _, name := range s.order {
		result = append(result, worker.FeatureStat{Name: name, PassCount: s.counts[name]})
	}
	return result
}

// rawJSON converts raw JSON into a value suitable for rebroadcasting.
func rawJSON(raw json.RawMessage) any {
	if len(raw) == 0 {
		return nil
	}
	var value any
	if err := json.Unmarshal(raw, &value); err != nil {
		return nil
	}
	return value
}

// round1 rounds a float to one decimal place.
func round1(value float64) float64 {
	return math.Round(value*10) / 10
}

// writeJSON writes a JSON response with the given status code.
func writeJSON(w http.ResponseWriter, status int, value any) {
	w.Header().Set("Content-Type", "application/json; charset=utf-8")
	w.WriteHeader(status)
	if err := json.NewEncoder(w).Encode(value); err != nil {
		log.Printf("write json response: %v", err)
	}
}

// String formats the seed range for logs and diagnostics.
func (r seedRange) String() string {
	return fmt.Sprintf("%d-%d", r.start, r.end)
}
