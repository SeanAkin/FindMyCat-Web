describe('authentication', () => {
  it('redirects an unauthenticated visitor to the login page', () => {
    cy.intercept('GET', '/auth/session', { statusCode: 401, body: {} }).as(
      'session',
    )

    cy.visit('/')
    cy.wait('@session')

    cy.location('pathname').should('eq', '/login')
    cy.contains('Sign in to FindMyCat')
  })

  it('shows the app for an authenticated visitor and logs out from the account menu', () => {
    cy.intercept('GET', '/auth/session', {
      statusCode: 200,
      body: {
        id: '1',
        email: 'cat.owner@example.com',
        displayName: 'Cat Owner',
        role: 'User',
      },
    }).as('session')
    cy.intercept('GET', '/api/devices', { statusCode: 200, body: [] }).as(
      'devices',
    )
    cy.intercept('POST', '/auth/logout', { statusCode: 204 }).as('logout')

    cy.visit('/')
    cy.wait('@session')

    cy.location('pathname').should('eq', '/')
    cy.contains('Devices')

    cy.get('[aria-label="Account menu"]').click()
    cy.contains('Cat Owner')
    cy.contains('cat.owner@example.com')
    cy.contains('[data-slot="dropdown-menu-item"]', 'Log out').click()
    cy.wait('@logout')

    cy.location('pathname').should('eq', '/login')
  })

  it('blocks a non-admin from reaching the admin page', () => {
    cy.intercept('GET', '/auth/session', {
      statusCode: 200,
      body: {
        id: '1',
        email: 'cat.owner@example.com',
        displayName: 'Cat Owner',
        role: 'User',
      },
    }).as('session')
    cy.intercept('GET', '/api/devices', { statusCode: 200, body: [] }).as(
      'devices',
    )

    cy.visit('/admin')
    cy.wait('@session')

    cy.location('pathname').should('eq', '/')
  })

  it('shows the access-denied screen after a blocked sign-in attempt', () => {
    cy.visit('/login?error=access_denied')

    cy.contains('Access not granted')
    cy.contains('Sign in with Google').should('not.exist')
  })
})
