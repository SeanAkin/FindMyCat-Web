function stubAuthenticatedSession() {
  cy.intercept('GET', '/auth/session', {
    statusCode: 200,
    body: {
      id: '1',
      email: 'cat.owner@example.com',
      displayName: 'Cat Owner',
      role: 'User',
    },
  }).as('session')
}

const device = {
  id: 1,
  name: 'Nova',
  uniqueId: 'unique-1',
  status: 'online',
  lastUpdate: new Date().toISOString(),
  disabled: false,
  position: {
    deviceId: 1,
    fixTime: new Date().toISOString(),
    deviceTime: new Date().toISOString(),
    serverTime: new Date().toISOString(),
    latitude: 51.5074,
    longitude: -0.1278,
    altitude: 10,
    speedKnots: 1.2,
    course: 90,
    accuracy: 5,
    valid: true,
    batteryLevel: 80,
    satellites: 8,
  },
}

function visitWithSelectedDevice() {
  stubAuthenticatedSession()
  cy.intercept('GET', '/api/devices', { statusCode: 200, body: [device] }).as(
    'devices',
  )

  cy.visit('/')
  cy.wait('@session')
  cy.wait('@devices')
  cy.contains('Nova').click()
}

describe('device commands', () => {
  it('sends a ping immediately, no confirmation dialog', () => {
    visitWithSelectedDevice()
    cy.intercept('POST', '/api/devices/1/ping', { statusCode: 204 }).as('ping')

    cy.contains('button', 'Ping').click()

    cy.wait('@ping')
    cy.get('[role="alertdialog"]').should('not.exist')
    cy.contains('Ping sent to Nova.')
  })

  it('requires confirmation before sending Mark Lost, then shows the lost-mode intent badge', () => {
    visitWithSelectedDevice()
    cy.intercept('POST', '/api/devices/1/lost', { statusCode: 204 }).as('lost')

    cy.contains('button', 'Mark Lost').click()
    cy.get('[role="alertdialog"]').should('be.visible')

    cy.get('[role="alertdialog"]').contains('button', 'Mark Lost').click()

    cy.wait('@lost')
    cy.contains('Lost mode command sent to Nova.')
    cy.contains('Lost mode command sent')
  })

  it('cancelling the confirmation dialog does not send Mark Active', () => {
    visitWithSelectedDevice()
    cy.intercept('POST', '/api/devices/1/active', {
      statusCode: 204,
    }).as('active')

    cy.contains('button', 'Mark Active').click()
    cy.get('[role="alertdialog"]').contains('button', 'Cancel').click()

    cy.get('[role="alertdialog"]').should('not.exist')
    cy.get('@active.all').should('have.length', 0)
  })

  it('maps a hologram error to a clear toast message', () => {
    visitWithSelectedDevice()
    cy.intercept('POST', '/api/devices/1/ping', {
      statusCode: 409,
      body: {
        code: 'hologram_not_configured',
        message: 'Hologram is not configured.',
      },
    }).as('ping')

    cy.contains('button', 'Ping').click()

    cy.wait('@ping')
    cy.contains('Collar commands are not set up yet')
  })
})
