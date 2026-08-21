export interface SessionResponse {
  id: string
  email: string
  displayName: string
  role: UserRole
}

export interface PositionResponse {
  deviceId: number
  fixTime: string
  deviceTime: string
  serverTime: string
  latitude: number
  longitude: number
  altitude: number
  speedKnots: number
  course: number
  accuracy: number
  valid: boolean
  batteryLevel: number | null
  satellites: number | null
}

export type DeviceStatus = 'online' | 'offline' | 'unknown'

export interface DeviceResponse {
  id: number
  name: string
  uniqueId: string
  status: DeviceStatus
  lastUpdate: string | null
  disabled: boolean
  position: PositionResponse | null
}

export interface CredentialStatusResponse {
  traccarConfigured: boolean
  hologramConfigured: boolean
}

export interface AllowedEmailResponse {
  email: string
  addedAt: string
}

export type UserRole = 'User' | 'Administrator'

export interface UserResponse {
  id: string
  email: string
  displayName: string
  role: UserRole
  isPrimaryAdministrator: boolean
  createdAt: string
  lastLoginAt: string
}

export type ApiErrorCode =
  | 'invalid_range'
  | 'range_too_large'
  | 'traccar_not_configured'
  | 'traccar_credential_rejected'
  | 'traccar_unavailable'
  | 'hologram_not_configured'
  | 'hologram_device_not_found'
  | 'hologram_credential_rejected'
  | 'hologram_unavailable'
  | 'primary_administrator_protected'
  | 'not_allow_listed'
  | 'email_already_registered'
  | 'weak_password'
  | 'invalid_credentials'
  | 'email_registered_with_password'
