describe('smoke', () => {
  it('loads the app and routes an unauthenticated visitor to sign in', () => {
    cy.intercept('GET', '/auth/session', { statusCode: 401, body: {} }).as(
      'session',
    )

    cy.visit('/')
    cy.wait('@session')

    cy.location('pathname').should('eq', '/login')
    cy.contains('Sign in to FindMyCat')
  })
})
