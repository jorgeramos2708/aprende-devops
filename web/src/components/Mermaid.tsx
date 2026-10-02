import { useEffect, useRef } from 'react'
import mermaid from 'mermaid'

mermaid.initialize({
  startOnLoad: false,
  theme: 'base',
  themeVariables: {
    primaryColor: '#0ea5e9',
    primaryTextColor: '#0f172a',
    primaryBorderColor: '#0ea5e9',
    lineColor: '#64748b',
    secondaryColor: '#1e293b',
    tertiaryColor: '#334155',
    background: '#0f172a',
    mainBkg: '#1e293b',
    secondBkg: '#334155',
    tertiaryBkg: '#475569',
  },
  flowchart: {
    useMaxWidth: true,
    htmlLabels: true,
    curve: 'basis',
  },
})

export function Mermaid({ chart }: { chart: string }) {
  const idRef = useRef(`mermaid-${Math.random().toString(36).slice(2)}`)

  useEffect(() => {
    const id = idRef.current
    const element = document.getElementById(id)
    if (!element) return

    mermaid.render(id, chart).then(({ svg }) => {
      element.innerHTML = svg
    }).catch((err) => {
      element.innerHTML = `<div class="text-red-500 p-4">Error rendering diagram: ${err.message}</div>`
    })
  }, [chart])

  return <div id={idRef.current} className="mermaid-container my-4" />
}
