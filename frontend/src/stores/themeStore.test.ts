import { beforeEach, describe, expect, it, vi } from 'vitest'

function stubMatchMedia(matchesDark: boolean) {
  window.matchMedia = ((query: string) => ({
    matches: query.includes('dark') && matchesDark,
    media: query,
    onchange: null,
    addEventListener: () => {},
    removeEventListener: () => {},
    addListener: () => {},
    removeListener: () => {},
    dispatchEvent: () => false,
  })) as typeof window.matchMedia
}

describe('themeStore', () => {
  beforeEach(() => {
    localStorage.clear()
    document.documentElement.classList.remove('dark')
    vi.resetModules()
    stubMatchMedia(false)
  })

  it('defaults to system theme and resolves against prefers-color-scheme', async () => {
    stubMatchMedia(true)
    const { useThemeStore } = await import('./themeStore')

    expect(useThemeStore.getState().theme).toBe('system')
    expect(useThemeStore.getState().resolvedTheme).toBe('dark')
    expect(document.documentElement.classList.contains('dark')).toBe(true)
  })

  it('setTheme persists to localStorage and toggles the dark class', async () => {
    const { useThemeStore } = await import('./themeStore')

    useThemeStore.getState().setTheme('dark')
    expect(document.documentElement.classList.contains('dark')).toBe(true)
    expect(localStorage.getItem('findmycat-theme')).toBe('dark')

    useThemeStore.getState().setTheme('light')
    expect(document.documentElement.classList.contains('dark')).toBe(false)
    expect(localStorage.getItem('findmycat-theme')).toBe('light')
  })

  it('reads a persisted theme on init', async () => {
    localStorage.setItem('findmycat-theme', 'dark')
    const { useThemeStore } = await import('./themeStore')

    expect(useThemeStore.getState().theme).toBe('dark')
    expect(document.documentElement.classList.contains('dark')).toBe(true)
  })
})
