describe('smoke', () => {
  it('loads the app and routes an unauthenticated visitor to sign in', () => {
    cy.visit('/')
    cy.location('pathname').should('eq', '/login')
    cy.contains('Sign in to FindMyCat')
  })
})
