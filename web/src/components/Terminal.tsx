import { useEffect, useRef, useState, useCallback } from 'react'
import { Terminal as XTerm } from 'xterm'
import { FitAddon } from 'xterm-addon-fit'
import { WebLinksAddon } from 'xterm-addon-web-links'
import { SearchAddon } from 'xterm-addon-search'
import { Maximize2, Minimize2, Copy, AlertCircle, Loader2 } from 'lucide-react'
import clsx from 'clsx'
import { api } from '../lib/api'

interface TerminalProps {
  attemptId: string
  cols?: number
  rows?: number
  onConnect?: () => void
  onDisconnect?: () => void
  onError?: (error: string) => void
}

export function Terminal({ attemptId, onConnect, onDisconnect, onError }: TerminalProps) {
  const containerRef = useRef<HTMLDivElement>(null)
  const termRef = useRef<XTerm | null>(null)
  const fitAddonRef = useRef<FitAddon>(new FitAddon())
  const websocketRef = useRef<WebSocket | null>(null)
  const [connected, setConnected] = useState(false)
  const [connecting, setConnecting] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [maximized, setMaximized] = useState(false)
  const resizeObserverRef = useRef<ResizeObserver | null>(null)

  const connect = useCallback(async () => {
    if (websocketRef.current?.readyState === WebSocket.OPEN) return
    
    setConnecting(true)
    setError(null)

    try {
      // Get terminal token from API (axios inyecta el JWT; fetch nativo lo omitia -> 401)
      const { data: tokenData } = await api.get(`/labs/${attemptId}/terminal/token`)
      const { webSocketUrl } = tokenData

      // webSocketUrl ya incluye el token (/api/labs/ws?token=...): usar tal cual
      const wsProtocol = window.location.protocol === 'https:' ? 'wss:' : 'ws:'
      const sep = webSocketUrl.includes('?') ? '&' : '?'
      const wsUrl = `${wsProtocol}//${window.location.host}${webSocketUrl}${sep}client=xterm`

      websocketRef.current = new WebSocket(wsUrl)
      const ws = websocketRef.current

      const term = new XTerm({
        cursorBlink: true,
        fontSize: 14,
        fontFamily: 'JetBrains Mono, Fira Code, monospace',
        theme: {
          background: '#0f172a',
          foreground: '#e2e8f0',
          cursor: '#0ea5e9',
          cursorAccent: '#0f172a',
          selectionBackground: 'rgba(14, 165, 233, 0.3)',
          black: '#1e293b',
          red: '#f87171',
          green: '#4ade80',
          yellow: '#facc15',
          blue: '#60a5fa',
          magenta: '#f472b6',
          cyan: '#22d3ee',
          white: '#f1f5f9',
          brightBlack: '#64748b',
          brightRed: '#fca5a5',
          brightGreen: '#86efac',
          brightYellow: '#fde047',
          brightBlue: '#93c5fd',
          brightMagenta: '#f9a8d4',
          brightCyan: '#67e8f9',
          brightWhite: '#f8fafc',
        },
        convertEol: true,
        scrollback: 10000,
        allowProposedApi: true,
      })

      term.loadAddon(fitAddonRef.current)
      term.loadAddon(new WebLinksAddon())
      term.loadAddon(new SearchAddon())

      if (containerRef.current) {
        term.open(containerRef.current)
        fitAddonRef.current.fit()
      }

      termRef.current = term

      ws.onopen = () => {
        setConnected(true)
        setConnecting(false)
        onConnect?.()
        
        // Send initial resize
        ws.send(JSON.stringify({ type: 'resize', cols: term.cols, rows: term.rows }))
      }

      ws.onmessage = (event) => {
        try {
          const data = JSON.parse(event.data)
          if (data.type === 'output') {
            term.write(data.data)
          } else if (data.type === 'error') {
            term.write(`\r\n\x1b[31m[Error: ${data.message}]\x1b[0m\r\n`)
          }
        } catch {
          // Raw text output
          term.write(event.data)
        }
      }

      ws.onclose = () => {
        setConnected(false)
        onDisconnect?.()
        term.write('\r\n\x1b[33m[Conexión cerrada]\x1b[0m\r\n')
      }

      ws.onerror = () => {
        setError('Error de conexión WebSocket')
        onError?.('WebSocket error')
      }

      // Handle terminal input
      term.onData((data: string) => {
        if (ws.readyState === WebSocket.OPEN) {
          ws.send(JSON.stringify({ type: 'input', data }))
        }
      })

      // Handle resize
      const handleResize = () => {
        if (containerRef.current && termRef.current) {
          fitAddonRef.current.fit()
          const { cols, rows } = termRef.current
          if (ws.readyState === WebSocket.OPEN) {
            ws.send(JSON.stringify({ type: 'resize', cols, rows }))
          }
        }
      }

      resizeObserverRef.current = new ResizeObserver(handleResize)
      if (containerRef.current) {
        resizeObserverRef.current.observe(containerRef.current)
      }

    } catch (err) {
      setError(err instanceof Error ? err.message : 'Error al conectar')
      setConnecting(false)
      onError?.(err instanceof Error ? err.message : 'Error al conectar')
    }
  }, [attemptId, onConnect, onDisconnect, onError])

  const disconnect = useCallback(() => {
    if (websocketRef.current) {
      websocketRef.current.close()
      websocketRef.current = null
    }
    if (termRef.current) {
      termRef.current.dispose()
      termRef.current = null
    }
    setConnected(false)
  }, [])

  const toggleMaximize = useCallback(() => {
    setMaximized(!maximized)
  }, [maximized])

  const copySelection = useCallback(() => {
    const selection = termRef.current?.getSelection()
    if (selection) {
      navigator.clipboard.writeText(selection)
    }
  }, [])

  useEffect(() => {
    connect()
    return () => {
      disconnect()
      if (resizeObserverRef.current) {
        resizeObserverRef.current.disconnect()
      }
    }
  }, [connect, disconnect])

  if (error && !connected) {
    return (
      <div className="card h-full flex flex-col" style={{ maxHeight: maximized ? 'calc(100vh - 120px)' : '500px' }}>
        <div className="flex items-center justify-between p-3 border-b border-dark-200 bg-dark-50">
          <div className="flex items-center gap-2">
            <div className="flex gap-1.5">
              <div className="w-3 h-3 rounded-full bg-red-500" />
              <div className="w-3 h-3 rounded-full bg-yellow-500" />
              <div className="w-3 h-3 rounded-full bg-green-500" />
            </div>
            <span className="font-mono text-sm text-dark-500">terminal</span>
          </div>
          <div className="flex items-center gap-1">
            <button onClick={toggleMaximize} className="p-1.5 rounded hover:bg-dark-200" title={maximized ? 'Minimizar' : 'Maximizar'}>
              {maximized ? <Minimize2 className="w-4 h-4" /> : <Maximize2 className="w-4 h-4" />}
            </button>
            <button onClick={connect} className="p-1.5 rounded hover:bg-dark-200" title="Reintentar">
              <Loader2 className="w-4 h-4 animate-spin" />
            </button>
          </div>
        </div>
        <div className="flex-1 flex items-center justify-center p-8">
          <div className="text-center">
            <AlertCircle className="w-12 h-12 text-red-500 mx-auto mb-4" />
            <h3 className="text-lg font-medium text-dark-900 mb-2">Error de conexión</h3>
            <p className="text-dark-600 mb-4">{error}</p>
            <button onClick={connect} className="btn-primary">
              <Loader2 className="w-4 h-4 mr-2" />
              Reintentar conexión
            </button>
          </div>
        </div>
      </div>
    )
  }

  return (
    <div className={clsx(
      'card relative overflow-hidden flex flex-col',
      maximized && 'fixed inset-4 z-50 lg:inset-8 rounded-xl shadow-2xl',
      !maximized && 'h-[500px]'
    )}>
      <div className="flex items-center justify-between p-3 border-b border-dark-200 bg-dark-50 flex-shrink-0">
        <div className="flex items-center gap-2">
          <div className="flex gap-1.5">
            <div className={clsx('w-3 h-3 rounded-full', connected ? 'bg-green-500' : 'bg-gray-400')} />
            <div className="w-3 h-3 rounded-full bg-yellow-500" />
            <div className="w-3 h-3 rounded-full bg-red-500" />
          </div>
          <span className="font-mono text-sm text-dark-500">terminal</span>
          {connected && <span className="text-xs text-green-600 font-medium">Conectado</span>}
          {connecting && <span className="text-xs text-yellow-600 font-medium animate-pulse">Conectando...</span>}
        </div>
        <div className="flex items-center gap-1">
          <button 
            onClick={copySelection} 
            className="p-1.5 rounded hover:bg-dark-200" 
            title="Copiar selección"
            disabled={!connected}
          >
            <Copy className="w-4 h-4" />
          </button>
          <button onClick={toggleMaximize} className="p-1.5 rounded hover:bg-dark-200" title={maximized ? 'Minimizar' : 'Maximizar'}>
            {maximized ? <Minimize2 className="w-4 h-4" /> : <Maximize2 className="w-4 h-4" />}
          </button>
          {!connected && !connecting && (
            <button onClick={connect} className="p-1.5 rounded hover:bg-dark-200 text-primary-600" title="Conectar">
              <Loader2 className="w-4 h-4" />
            </button>
          )}
        </div>
      </div>

      <div 
        ref={containerRef} 
        className="flex-1 overflow-hidden bg-dark-950 relative"
        style={{ 
          height: maximized ? 'calc(100% - 48px)' : 'calc(100% - 48px)',
          minHeight: 300 
        }}
      >
        {!connected && !connecting && (
          <div className="absolute inset-0 flex flex-col items-center justify-center p-8 text-center text-dark-500">
            <TerminalIcon className="w-16 h-16 mb-4 opacity-50" />
            <p className="text-lg font-medium text-dark-400 mb-2">Terminal desconectado</p>
            <p className="text-sm mb-6">Haz clic en "Conectar" para iniciar la sesión de laboratorio</p>
            <button onClick={connect} className="btn-primary">
              <Loader2 className="w-4 h-4 mr-2" />
              Conectar al laboratorio
            </button>
          </div>
        )}
      </div>

      {connected && (
        <div className="absolute bottom-2 right-2 flex gap-1 opacity-0 hover:opacity-100 transition-opacity pointer-events-none group">
          <div className="group-hover:pointer-events-auto">
            <span className="absolute bottom-full right-0 mb-2 px-2 py-1 bg-dark-900 text-dark-100 text-xs rounded whitespace-nowrap opacity-0 group-hover:opacity-100 transition-opacity">
              Teclas útiles: Ctrl+C (interrumpir), Ctrl+L (limpiar), Tab (autocompletar)
            </span>
          </div>
        </div>
      )}
    </div>
  )
}

// Terminal icon component
function TerminalIcon({ className }: { className?: string }) {
  return (
    <svg className={className} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5">
      <path d="M8 9h8" />
      <path d="M8 15h8" />
      <path d="M12 3v18" />
      <rect x="3" y="3" width="18" height="18" rx="2" />
    </svg>
  )
}
