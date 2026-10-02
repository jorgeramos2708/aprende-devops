import React from 'react'
import MarkdownIt from 'markdown-it'
import hljs from 'highlight.js'
import 'highlight.js/styles/github-dark.min.css'
import { Mermaid } from './Mermaid'

const md = new MarkdownIt({
  html: true,
  linkify: true,
  typographer: true,
  highlight: (str, lang) => {
    if (lang && hljs.getLanguage(lang)) {
      try {
        return `<pre class="hljs"><code>${hljs.highlight(str, { language: lang, ignoreIllegals: true }).value}</code></pre>`
      } catch (__) {}
    }
    return `<pre class="hljs"><code>${md.utils.escapeHtml(str)}</code></pre>`
  },
})

// Custom renderers for admonitions, tabs, etc.
const defaultRender = md.renderer.rules.fence || function(tokens, idx, options, env, self) {
  return self.renderToken(tokens, idx, options)
}

md.renderer.rules.fence = (tokens, idx, options, env, self) => {
  const token = tokens[idx]
  const info = token.info.trim()
  
  // Mermaid diagrams
  if (info === 'mermaid') {
    return `<div class="mermaid">${token.content}</div>`
  }
  
  // Admonitions: ::: note, ::: warning, ::: danger, ::: tip
  if (info.startsWith('note') || info.startsWith('warning') || info.startsWith('danger') || info.startsWith('tip')) {
    const type = info.split(' ')[0]
    const title = info.split(' ').slice(1).join(' ') || type.charAt(0).toUpperCase() + type.slice(1)
    const icons = {
      note: 'ℹ️',
      tip: '💡',
      warning: '⚠️',
      danger: '🚨',
    }
    return `
      <div class="admonition admonition-${type}">
        <div class="admonition-header">
          <span class="admonition-icon">${icons[type as keyof typeof icons] || '📝'}</span>
          <span class="admonition-title">${title}</span>
        </div>
        <div class="admonition-content">${md.renderInline(token.content)}</div>
      </div>
    `
  }

  return defaultRender(tokens, idx, options, env, self)
}

// Task list items
md.renderer.rules.listitem = (tokens, idx, options, env, self) => {
  const token = tokens[idx]
  if (token.children) {
    const firstChild = token.children[0]
    if (firstChild && firstChild.type === 'paragraph') {
      const text = firstChild.children?.[0]?.content || ''
      if (text.startsWith('[ ] ') || text.startsWith('[x] ') || text.startsWith('[X] ')) {
        const checked = text[1] === 'x' || text[1] === 'X'
        const content = text.slice(3)
        firstChild.children[0].content = content
        return `<li class="task-list-item"><input type="checkbox" ${checked ? 'checked' : ''} disabled /> ${self.renderToken(tokens, idx, options)}</li>`
      }
    }
  }
  return md.renderer.rules.listitem!(tokens, idx, options, env, self)
}

export function MarkdownRenderer({ content, className }: { content: string; className?: string }) {
  const html = md.render(content)
  
  return (
    <div className={clsx('prose-content', className)} dangerouslySetInnerHTML={{ __html: html }} />
  )
}

import clsx from 'clsx'
