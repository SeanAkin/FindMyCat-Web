import { ErrorAlert } from '@/components/ErrorAlert'
import { getTraccarErrorMessage } from '@/lib/deviceErrors'
import { selectTraccarProblem, useDevicesStore } from '@/stores/devicesStore'

export function TraccarStatusAlert() {
  const problem = useDevicesStore(selectTraccarProblem)
  const hasLoadedDevices = useDevicesStore(
    (state) => state.status === 'success',
  )
  if (!problem || !hasLoadedDevices) return null

  const message = getTraccarErrorMessage(problem)
  return (
    <ErrorAlert
      title={message.title}
      description={message.description}
      variant={message.variant === 'error' ? 'destructive' : 'default'}
      adminActionable={message.adminActionable}
    />
  )
}
