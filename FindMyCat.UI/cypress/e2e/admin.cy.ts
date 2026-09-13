function stubAdminSession() {
  cy.intercept('GET', '/auth/session', {
    statusCode: 200,
    body: {
      id: '1',
      email: 'admin@example.com',
      displayName: 'Household Admin',
      role: 'Administrator',
    },
  }).as('session')
}

function visitAdminPage() {
  stubAdminSession()
  cy.intercept('GET', '/api/admin/allowed-emails', {
    statusCode: 200,
    body: [{ email: 'admin@example.com', addedAt: new Date().toISOString() }],
  }).as('allowedEmails')
  cy.intercept('GET', '/api/admin/users', {
    statusCode: 200,
    body: [
      {
        id: 'user-1',
        email: 'admin@example.com',
        displayName: 'Household Admin',
        role: 'Administrator',
        isPrimaryAdministrator: true,
        createdAt: new Date().toISOString(),
        lastLoginAt: new Date().toISOString(),
      },
    ],
  }).as('users')
  cy.intercept('GET', '/api/credentials', {
    statusCode: 200,
    body: { traccarConfigured: false, hologramConfigured: false },
  }).as('credentials')

  cy.visit('/admin')
  cy.wait('@session')
  cy.wait('@allowedEmails')
  cy.wait('@users')
  cy.wait('@credentials')
}

describe('admin area', () => {
  it('adds an allowed email', () => {
    visitAdminPage()
    cy.intercept('POST', '/api/admin/allowed-emails', {
      statusCode: 200,
      body: { email: 'new@example.com', addedAt: new Date().toISOString() },
    }).as('addAllowedEmail')
    cy.intercept('GET', '/api/admin/allowed-emails', {
      statusCode: 200,
      body: [
        { email: 'admin@example.com', addedAt: new Date().toISOString() },
        { email: 'new@example.com', addedAt: new Date().toISOString() },
      ],
    }).as('refreshedAllowedEmails')

    cy.get('input[placeholder="name@example.com"]').type('new@example.com')
    cy.contains('button', 'Add email').click()

    cy.wait('@addAllowedEmail')
    cy.wait('@refreshedAllowedEmails')
    cy.contains('new@example.com can now sign in.')
    cy.contains('new@example.com')
  })

  it('sets a Traccar credential', () => {
    visitAdminPage()
    cy.intercept('PUT', '/api/credentials/traccar', {
      statusCode: 204,
    }).as('setTraccar')
    cy.intercept('GET', '/api/credentials', {
      statusCode: 200,
      body: { traccarConfigured: true, hologramConfigured: false },
    }).as('refreshedCredentials')

    cy.get('input[placeholder="Traccar API token"]').type('fake-token')
    cy.contains('Traccar API token')
      .parents('div.rounded-lg')
      .contains('button', 'Set')
      .click()

    cy.wait('@setTraccar')
    cy.wait('@refreshedCredentials')
    cy.contains('Traccar API token saved.')
    cy.get('input[placeholder="Traccar API token"]').should('have.value', '')
  })

  it('shows an "Admin settings" nav link for an admin that navigates to /admin', () => {
    stubAdminSession()
    cy.intercept('GET', '/api/devices', { statusCode: 200, body: [] }).as(
      'devices',
    )
    cy.intercept('GET', '/api/admin/allowed-emails', {
      statusCode: 200,
      body: [],
    }).as('allowedEmails')
    cy.intercept('GET', '/api/admin/users', { statusCode: 200, body: [] }).as(
      'users',
    )
    cy.intercept('GET', '/api/credentials', {
      statusCode: 200,
      body: { traccarConfigured: false, hologramConfigured: false },
    }).as('credentials')

    cy.visit('/')
    cy.wait('@session')
    cy.wait('@devices')

    cy.get('[aria-label="Admin settings"]').click()

    cy.location('pathname').should('eq', '/admin')
    cy.wait('@allowedEmails')
  })
})
