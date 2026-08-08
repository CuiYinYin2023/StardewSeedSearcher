package main

import (
	"context"
	"log"
	"os"
	"os/signal"
	"runtime"
	"stardewseedsearcher/internal/server"
	"stardewseedsearcher/internal/worker"
	"strconv"
	"strings"
	"syscall"
)

func main() {
	ctx, stop := signal.NotifyContext(context.Background(), os.Interrupt, syscall.SIGTERM)
	defer stop()

	workerCount := runtime.NumCPU() - 1
	if workerCount < 1 {
		workerCount = 1
	}
	if raw := strings.TrimSpace(os.Getenv("SEED_GO_WORKERS")); raw != "" {
		if parsed, err := strconv.Atoi(raw); err == nil && parsed > 0 {
			workerCount = parsed
		}
	}

	pool, err := worker.NewPool(ctx, workerCount)
	if err != nil {
		log.Fatalf("start C# workers: %v", err)
	}
	defer pool.Close()

	// set addr via os.env
	addr := strings.TrimSpace(os.Getenv("SEED_GO_ADDR"))
	if addr == "" {
		addr = "localhost:5050"
	}

	app := server.New(pool)
	log.Printf("Go+C# hybrid backend listening on http://%s with %d C# workers", addr, pool.Len())
	if err := app.ListenAndServe(ctx, addr); err != nil {
		log.Fatal(err)
	}
}
