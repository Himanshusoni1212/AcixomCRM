# UI_UX_DESIGN (§12 + user style bans)

## Standards from assignment
- Responsive Bootstrap layout for desktop + tablet (+ mobile – see ACCESSIBILITY_SEO_PERFORMANCE).
- Consistent nav across modules; role-based menu; common layout header/user-menu/notifications/footer.
- Inline validation next to fields; clear success/error/warning toasts; confirm dialogs for destructive actions.
- Search/filter/sort/pagination on all lists. Accessible labels, keyboard-friendly controls, meaningful messages. Never expose stack traces.

## User-mandated style (no vibrant)
NEVER use (see CONTENT_GUARDRAILS for enforcement):
- Purple gradients, fill-shift buttons, emoji icons, cursor animation, over-the-top scroll animation, AI slug photos/copy, fake reviews/metrics/hero text/counters, em-dashes in copy (use commas/colons), made-with-AI tag.

DO use:
- Neutral professional palette (slate/navy + white + one restrained accent e.g. teal/blue). Solid buttons with clear labels (“Save Customer”, “Schedule Follow-Up”, “Mark Won”).
- Real business details in footer/header: business name, address, phone (`tel:`), email (`mailto:`), hours. Logo clickable to `/dashboard`.
- Custom real photo per page (compressed, licensed, alt text) – no stock AI slop, no copyrighted images without license.
- Favicon (.ico + png + apple-touch), proper `<title>` + meta description per page, working footer links, mobile hamburger menu, no horizontal scroll, no mobile overflow.
- Forms: `<label>` for every input, focus visible, error `aria-describedby`, success (“Customer saved.”) + error (“Fix email format.”) messages, no placeholder-as-label, no lorem/placeholder text in prod.

## Page list (minimum)
Login, Register, Dashboard, Customers (list/form/detail), Leads (list/form/convert), Opportunities (list/form/pipeline), Follow-Ups (upcoming/overdue/form), Activities, Users/Roles (Admin), Audit Log, Reports, Privacy, Terms, Cookie Policy, Refund Policy, 404/403/500.
