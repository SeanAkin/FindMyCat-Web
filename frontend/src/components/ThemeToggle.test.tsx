import { beforeEach, describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { ThemeToggle } from './ThemeToggle'
import { useThemeStore } from '@/stores/themeStore'

describe('ThemeToggle', () => {
  beforeEach(() => {
    useThemeStore.getState().setTheme('light')
  })

  it('switches to dark and applies the dark class', async () => {
    const user = userEvent.setup()
    render(<ThemeToggle />)

    await user.click(screen.getByRole('button', { name: 'Change theme' }))
    await user.click(await screen.findByText('Dark'))

    expect(useThemeStore.getState().theme).toBe('dark')
    expect(document.documentElement.classList.contains('dark')).toBe(true)
  })
})
