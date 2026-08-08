package ws

import (
	"bufio"
	"crypto/sha1"
	"encoding/base64"
	"encoding/binary"
	"encoding/json"
	"fmt"
	"io"
	"net"
	"net/http"
	"strings"
	"sync"
)

const acceptGUID = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11"

type Hub struct {
	mu    sync.Mutex
	conns map[*Conn]struct{}
}

// NewHub creates an empty WebSocket connection hub.
func NewHub() *Hub {
	return &Hub{conns: make(map[*Conn]struct{})}
}

// ServeHTTP upgrades HTTP requests and registers WebSocket clients.
func (h *Hub) ServeHTTP(w http.ResponseWriter, r *http.Request) {
	if !strings.EqualFold(r.Header.Get("Upgrade"), "websocket") {
		http.Error(w, "websocket upgrade required", http.StatusBadRequest)
		return
	}

	key := r.Header.Get("Sec-WebSocket-Key")
	if key == "" {
		http.Error(w, "missing Sec-WebSocket-Key", http.StatusBadRequest)
		return
	}

	hijacker, ok := w.(http.Hijacker)
	if !ok {
		http.Error(w, "hijacking is not supported", http.StatusInternalServerError)
		return
	}

	conn, rw, err := hijacker.Hijack()
	if err != nil {
		return
	}

	accept := websocketAccept(key)
	_, _ = fmt.Fprintf(rw, "HTTP/1.1 101 Switching Protocols\r\n")
	_, _ = fmt.Fprintf(rw, "Upgrade: websocket\r\n")
	_, _ = fmt.Fprintf(rw, "Connection: Upgrade\r\n")
	_, _ = fmt.Fprintf(rw, "Sec-WebSocket-Accept: %s\r\n\r\n", accept)
	_ = rw.Flush()

	client := &Conn{conn: conn}
	h.add(client)
	go func() {
		defer func() {
			h.remove(client)
			_ = conn.Close()
		}()
		readLoop(rw.Reader, conn)
	}()
}

// Broadcast sends a JSON message to all connected clients.
func (h *Hub) Broadcast(value any) {
	payload, err := json.Marshal(value)
	if err != nil {
		return
	}

	h.mu.Lock()
	conns := make([]*Conn, 0, len(h.conns))
	for conn := range h.conns {
		conns = append(conns, conn)
	}
	h.mu.Unlock()

	for _, conn := range conns {
		if err := conn.SendText(payload); err != nil {
			h.remove(conn)
			_ = conn.conn.Close()
		}
	}
}

// add registers a connection in the hub.
func (h *Hub) add(conn *Conn) {
	h.mu.Lock()
	h.conns[conn] = struct{}{}
	h.mu.Unlock()
}

// remove unregisters a connection from the hub.
func (h *Hub) remove(conn *Conn) {
	h.mu.Lock()
	delete(h.conns, conn)
	h.mu.Unlock()
}

type Conn struct {
	conn net.Conn
	mu   sync.Mutex
}

// SendText writes one unmasked text frame to the client.
func (c *Conn) SendText(payload []byte) error {
	c.mu.Lock()
	defer c.mu.Unlock()

	header := make([]byte, 10)
	header[0] = 0x81
	length := len(payload)

	switch {
	case length <= 125:
		header[1] = byte(length)
		_, err := c.conn.Write(append(header[:2], payload...))
		return err
	case length <= 65535:
		header[1] = 126
		binary.BigEndian.PutUint16(header[2:4], uint16(length))
		if _, err := c.conn.Write(header[:4]); err != nil {
			return err
		}
	default:
		header[1] = 127
		binary.BigEndian.PutUint64(header[2:10], uint64(length))
		if _, err := c.conn.Write(header[:10]); err != nil {
			return err
		}
	}

	_, err := c.conn.Write(payload)
	return err
}

// websocketAccept computes the RFC 6455 accept key.
func websocketAccept(key string) string {
	sum := sha1.Sum([]byte(key + acceptGUID))
	return base64.StdEncoding.EncodeToString(sum[:])
}

// readLoop drains client frames until the socket closes.
func readLoop(reader *bufio.Reader, conn net.Conn) {
	for {
		first, err := reader.ReadByte()
		if err != nil {
			return
		}
		second, err := reader.ReadByte()
		if err != nil {
			return
		}

		opcode := first & 0x0f
		masked := second&0x80 != 0
		length := uint64(second & 0x7f)

		switch length {
		case 126:
			var buf [2]byte
			if _, err := io.ReadFull(reader, buf[:]); err != nil {
				return
			}
			length = uint64(binary.BigEndian.Uint16(buf[:]))
		case 127:
			var buf [8]byte
			if _, err := io.ReadFull(reader, buf[:]); err != nil {
				return
			}
			length = binary.BigEndian.Uint64(buf[:])
		}

		var mask [4]byte
		if masked {
			if _, err := io.ReadFull(reader, mask[:]); err != nil {
				return
			}
		}

		if _, err := io.CopyN(io.Discard, reader, int64(length)); err != nil {
			return
		}

		if opcode == 0x8 {
			_, _ = conn.Write([]byte{0x88, 0x00})
			return
		}
	}
}
