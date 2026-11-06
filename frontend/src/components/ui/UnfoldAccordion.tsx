// Adapted from EasyUI UnfoldAccordion.tsx. Copyright (c) 2026 Suraj Maurya.
// MIT license: ../../../THIRD_PARTY_NOTICES.md
import { useId, useState, type ReactNode } from 'react'
import { AnimatePresence, motion, useReducedMotion } from 'framer-motion'
import { ChevronDown } from 'lucide-react'

export function UnfoldAccordion({ title, subtitle, children }: {
  title: string; subtitle?: string; children: ReactNode
}) {
  const [open, setOpen] = useState(false)
  const id = useId()
  const reduced = useReducedMotion()
  return <div className="ui-accordion">
    <button type="button" id={`${id}-trigger`} aria-expanded={open} aria-controls={id} onClick={() => setOpen(!open)}>
      <span><strong>{title}</strong>{subtitle && <small>{subtitle}</small>}</span>
      <ChevronDown size={18} aria-hidden="true" className={open ? 'rotated' : ''} />
    </button>
    <AnimatePresence initial={false}>
      {open && <motion.div id={id} role="region" aria-labelledby={`${id}-trigger`}
        initial={reduced ? { opacity: 0 } : { height: 0, opacity: 0 }} animate={{ height: 'auto', opacity: 1 }}
        exit={reduced ? { opacity: 0 } : { height: 0, opacity: 0 }} transition={{ duration: .15 }} className="accordion-content">
        <div>{children}</div>
      </motion.div>}
    </AnimatePresence>
  </div>
}
