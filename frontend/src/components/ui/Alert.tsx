// Adapted from EasyUI alert.tsx. Copyright (c) 2026 Suraj Maurya.
// MIT license: ../../../THIRD_PARTY_NOTICES.md
import { useState, type ReactNode } from 'react'
import { AnimatePresence, motion, useReducedMotion } from 'framer-motion'
import { CircleAlert, CircleCheck, Info, TriangleAlert, X } from 'lucide-react'

type Tone = 'info' | 'success' | 'warning' | 'danger'
const icons = { info: Info, success: CircleCheck, warning: TriangleAlert, danger: CircleAlert }

export function Alert({ tone = 'info', children, dismissible = false }: {
  tone?: Tone; children: ReactNode; dismissible?: boolean
}) {
  const [visible, setVisible] = useState(true)
  const reduced = useReducedMotion()
  const Icon = icons[tone]
  return <AnimatePresence initial={false}>
    {visible && <motion.div initial={false} animate={{ opacity: 1, height: 'auto' }}
      exit={reduced ? { opacity: 0 } : { opacity: 0, height: 0 }} transition={{ duration: .15 }} className="alert-wrap">
      <div role={tone === 'danger' || tone === 'warning' ? 'alert' : 'status'} className={`ui-alert ui-alert-${tone}`}>
        <Icon size={20} aria-hidden="true" /><div>{children}</div>
        {dismissible && <button type="button" className="icon-button" aria-label="Cerrar mensaje" onClick={() => setVisible(false)}><X size={18} /></button>}
      </div>
    </motion.div>}
  </AnimatePresence>
}
