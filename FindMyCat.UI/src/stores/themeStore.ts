import { create } from 'zustand'

export type Theme = 'light' | 'dark' | 'system'
type ResolvedTheme = 'light' | 'dark'

const STORAGE_KEY = 'findmycat-theme'

function getSystemTheme(): ResolvedTheme {
  return window.matchMedia('(prefers-color-scheme: dark)').matches
    ? 'dark'
    : 'light'
}

function resolveTheme(theme: Theme): ResolvedTheme {
  return theme === 'system' ? getSystemTheme() : theme
}

function applyResolvedTheme(resolvedTheme: ResolvedTheme) {
  document.documentElement.classList.toggle('dark', resolvedTheme === 'dark')
}

function readStoredTheme(): Theme {
  const stored = localStorage.getItem(STORAGE_KEY)
  return stored === 'light' || stored === 'dark' || stored === 'system'
    ? stored
    : 'system'
}

interface ThemeState {
  theme: Theme
  resolvedTheme: ResolvedTheme
  setTheme: (theme: Theme) => void
}

const initialTheme = readStoredTheme()
const initialResolvedTheme = resolveTheme(initialTheme)
applyResolvedTheme(initialResolvedTheme)

export const useThemeStore = create<ThemeState>((set) => ({
  theme: initialTheme,
  resolvedTheme: initialResolvedTheme,
  setTheme: (theme) => {
    const resolvedTheme = resolveTheme(theme)
    localStorage.setItem(STORAGE_KEY, theme)
    applyResolvedTheme(resolvedTheme)
    set({ theme, resolvedTheme })
  },
}))

window
  .matchMedia('(prefers-color-scheme: dark)')
  .addEventListener('change', (event) => {
    if (useThemeStore.getState().theme !== 'system') return
    const resolvedTheme: ResolvedTheme = event.matches ? 'dark' : 'light'
    applyResolvedTheme(resolvedTheme)
    useThemeStore.setState({ resolvedTheme })
  })
