// @vitest-environment jsdom
import { describe, it, expect, afterEach } from 'vitest'
import { render, screen, fireEvent, cleanup } from '@testing-library/react'
import '@testing-library/jest-dom/vitest'
import { Button } from '../components/Button'

afterEach(() => cleanup())

describe('Button', () => {
  it('renderiza el texto y responde al click', () => {
    let clicks = 0
    render(<Button onClick={() => { clicks++ }}>Guardar</Button>)
    const btn = screen.getByRole('button', { name: 'Guardar' })
    fireEvent.click(btn)
    expect(clicks).toBe(1)
  })

  it('deshabilitado no responde', () => {
    render(<Button disabled>Guardar</Button>)
    expect(screen.getByRole('button', { name: 'Guardar' })).toBeDisabled()
  })
})
