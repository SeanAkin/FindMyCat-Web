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

describe('devices', () => {
  it('shows a populated device list and its detail panel on selection', () => {
    stubAuthenticatedSession()
    cy.intercept('GET', '/api/devices', { statusCode: 200, body: [device] }).as(
      'devices',
    )

    cy.visit('/')
    cy.wait('@session')
    cy.wait('@devices')

    cy.contains('Nova')
    cy.contains('Online')
    cy.contains('Select a device to see details')

    cy.contains('Nova').click()

    cy.contains('This collar has not reported a location yet.').should(
      'not.exist',
    )
    cy.contains('1.2 kn')
    cy.contains('80%')
  })

  it('shows the empty state when there are no devices', () => {
    stubAuthenticatedSession()
    cy.intercept('GET', '/api/devices', { statusCode: 200, body: [] }).as(
      'devices',
    )

    cy.visit('/')
    cy.wait('@session')
    cy.wait('@devices')

    cy.contains('No devices yet')
  })

  it('shows a setup nudge when Traccar is not configured', () => {
    stubAuthenticatedSession()
    cy.intercept('GET', '/api/devices', {
      statusCode: 409,
      body: {
        code: 'traccar_not_configured',
        message: 'Traccar is not configured.',
      },
    }).as('devices')

    cy.visit('/')
    cy.wait('@session')
    cy.wait('@devices')

    cy.contains('Location tracking is not set up yet')
    cy.contains('Try again').should('not.exist')
  })
})
