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

const onlineDevice = {
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

const offlineDevice = {
  id: 2,
  name: 'Whiskers',
  uniqueId: 'unique-2',
  status: 'offline',
  lastUpdate: new Date().toISOString(),
  disabled: false,
  position: {
    deviceId: 2,
    fixTime: new Date().toISOString(),
    deviceTime: new Date().toISOString(),
    serverTime: new Date().toISOString(),
    latitude: 51.51,
    longitude: -0.13,
    altitude: 8,
    speedKnots: 0,
    course: 0,
    accuracy: 6,
    valid: true,
    batteryLevel: 42,
    satellites: 6,
  },
}

describe('device map', () => {
  it('shows a marker per device and focuses the marker when selected from the list', () => {
    stubAuthenticatedSession()
    cy.intercept('GET', '/api/devices', {
      statusCode: 200,
      body: [onlineDevice, offlineDevice],
    }).as('devices')

    cy.visit('/')
    cy.wait('@session')
    cy.wait('@devices')

    cy.get('.device-map .leaflet-marker-icon').should('have.length', 2)
    cy.get('.device-marker--selected').should('not.exist')

    cy.contains('Nova').click()

    cy.get('.device-marker--selected').should('have.length', 1)
    cy.get('.leaflet-popup-content').should('contain.text', 'Nova')
  })

  it('shows a "no positions yet" message when devices have no position', () => {
    stubAuthenticatedSession()
    cy.intercept('GET', '/api/devices', {
      statusCode: 200,
      body: [{ ...onlineDevice, position: null }],
    }).as('devices')

    cy.visit('/')
    cy.wait('@session')
    cy.wait('@devices')

    cy.contains('No positions yet')
    cy.get('.leaflet-marker-icon').should('not.exist')
  })
})
