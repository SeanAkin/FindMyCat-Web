import { afterEach, describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import App from './App'
import { useAuthStore } from '@/stores/authStore'

describe('App', () => {
  afterEach(() => {
    useAuthStore.setState({ status: 'loading', user: null })
  })

  it('renders the app shell once authenticated', () => {
    useAuthStore.setState({
      status: 'authenticated',
      user: {
        id: '1',
        email: 'cat.owner@example.com',
        displayName: 'Cat Owner',
        role: 'User',
      },
    })

    render(<App />)

    expect(screen.getByText('FindMyCat')).toBeInTheDocument()
  })

  it('routes an unauthenticated visitor to the login page', () => {
    useAuthStore.setState({ status: 'unauthenticated', user: null })

    render(<App />)

    expect(screen.getByText('Sign in to FindMyCat')).toBeInTheDocument()
  })
})
