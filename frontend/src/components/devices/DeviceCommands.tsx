import type { ReactNode } from 'react'
import { LocateFixed, ShieldAlert, ShieldCheck } from 'lucide-react'
import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { toast } from 'sonner'
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger,
} from '@/components/ui/alert-dialog'
import { Button } from '@/components/ui/button'
import { markDeviceActive, markDeviceLost, pingDevice } from '@/api/devices'
import { toApiError } from '@/api/http'
import { getDeviceErrorMessage } from '@/lib/deviceErrors'
import { selectIsAdmin, useAuthStore } from '@/stores/authStore'
import { useDevicesStore } from '@/stores/devicesStore'

type CommandName = 'ping' | 'lost' | 'active'

interface DeviceCommandsProps {
  deviceId: number
  deviceName: string
}

interface ConfirmCommandDialogProps {
  icon: ReactNode
  buttonLabel: string
  pendingLabel: string
  isPending: boolean
  disabled: boolean
  title: string
  description: string
  onConfirm: () => void
}

function ConfirmCommandDialog({
  icon,
  buttonLabel,
  pendingLabel,
  isPending,
  disabled,
  title,
  description,
  onConfirm,
}: ConfirmCommandDialogProps) {
  return (
    <AlertDialog>
      <AlertDialogTrigger
        render={
          <Button variant="outline" size="sm" disabled={disabled}>
            {icon}
            {isPending ? pendingLabel : buttonLabel}
          </Button>
        }
      />
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>{title}</AlertDialogTitle>
          <AlertDialogDescription>{description}</AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel>Cancel</AlertDialogCancel>
          <AlertDialogAction onClick={onConfirm}>
            {buttonLabel}
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  )
}

export function DeviceCommands({ deviceId, deviceName }: DeviceCommandsProps) {
  const [pendingCommand, setPendingCommand] = useState<CommandName | null>(null)
  const setLastSentDeviceCommand = useDevicesStore(
    (state) => state.setLastSentDeviceCommand,
  )
  const isAdmin = useAuthStore(selectIsAdmin)
  const navigate = useNavigate()

  const runCommand = async (
    command: CommandName,
    send: () => Promise<void>,
    successMessage: string,
  ) => {
    setPendingCommand(command)
    try {
      await send()
      toast.success(successMessage)
      if (command === 'lost' || command === 'active') {
        setLastSentDeviceCommand(deviceId, command)
      }
    } catch (error) {
      const message = getDeviceErrorMessage(toApiError(error))
      toast.error(message.title, {
        description: message.description,
        action:
          message.adminActionable && isAdmin
            ? { label: 'Go to Admin', onClick: () => navigate('/admin') }
            : undefined,
      })
    } finally {
      setPendingCommand(null)
    }
  }

  return (
    <div className="flex flex-wrap gap-2">
      <Button
        variant="outline"
        size="sm"
        disabled={pendingCommand !== null}
        onClick={() =>
          void runCommand(
            'ping',
            () => pingDevice(deviceId),
            `Ping sent to ${deviceName}.`,
          )
        }
      >
        <LocateFixed data-icon="inline-start" />
        {pendingCommand === 'ping' ? 'Pinging…' : 'Ping'}
      </Button>

      <ConfirmCommandDialog
        icon={<ShieldAlert data-icon="inline-start" />}
        buttonLabel="Mark Lost"
        pendingLabel="Sending…"
        isPending={pendingCommand === 'lost'}
        disabled={pendingCommand !== null}
        title={`Mark ${deviceName} as lost?`}
        description="This sends a command that switches the collar to lost mode, which changes its reporting behaviour and uses more battery."
        onConfirm={() =>
          void runCommand(
            'lost',
            () => markDeviceLost(deviceId),
            `Lost mode command sent to ${deviceName}.`,
          )
        }
      />

      <ConfirmCommandDialog
        icon={<ShieldCheck data-icon="inline-start" />}
        buttonLabel="Mark Active"
        pendingLabel="Sending…"
        isPending={pendingCommand === 'active'}
        disabled={pendingCommand !== null}
        title={`Mark ${deviceName} as active?`}
        description="This sends a command that switches the collar back to normal reporting behaviour."
        onConfirm={() =>
          void runCommand(
            'active',
            () => markDeviceActive(deviceId),
            `Active mode command sent to ${deviceName}.`,
          )
        }
      />
    </div>
  )
}
