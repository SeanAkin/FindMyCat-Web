describe('theme toggle', () => {
  beforeEach(() => {
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
  })

  it('persists the selected theme across a reload', () => {
    cy.visit('/')
    cy.wait('@session')

    cy.get('[aria-label="Change theme"]').click()
    cy.contains('[data-slot="dropdown-menu-item"]', 'Dark').click()
    cy.get('html').should('have.class', 'dark')

    cy.reload()
    cy.get('html').should('have.class', 'dark')

    cy.get('[aria-label="Change theme"]').click()
    cy.contains('[data-slot="dropdown-menu-item"]', 'Light').click()
    cy.get('html').should('not.have.class', 'dark')

    cy.reload()
    cy.get('html').should('not.have.class', 'dark')
  })
})
