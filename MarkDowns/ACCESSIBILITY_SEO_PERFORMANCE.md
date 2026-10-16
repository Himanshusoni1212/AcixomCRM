# ACCESSIBILITY_SEO_PERFORMANCE — fix list

## Accessibility (WCAG 2.1 AA target)
- Alt text on every informative image; `alt=""` only for decorative; no image-of-text.
- Contrast >=4.5:1 body, >=3:1 large/UI; test with APAXE/Lighthouse; never light-grey on white.
- Keyboard: all nav/forms/buttons focusable, visible focus ring, logical tab order, Esc closes menu/dialog, skip-to-content link.
- Labels: `<label for>` on every input; `aria-describedby` for errors; clear button labels (“Save Customer”, “Delete Lead” with confirm).
- Mobile menu: hamburger button `aria-expanded`, focus trap, closes on link click.
- No cursor animation, no over-the-top scroll animation (vestibular + perf). Respect `prefers-reduced-motion`.

## SEO basics
- Unique `<title>` per page (e.g. “Customers | AcxiomCRM”), meta description 120–155 chars, canonical, OG tags, robots.txt + sitemap.xml.
- Semantic headings (one H1), descriptive link text (not “click here”), footer links all resolve (audit weekly for broken links).
- Favicon: `/favicon.ico` + `icon-192/512.png` + `apple-touch-icon.png` + manifest. Custom domain + HTTPS before launch.

## Performance / UX bugs to fix (from your list)
- [ ] No horizontal scrolling (max-width 100%, `overflow-x:hidden` only as guard + fix root cause, tables scroll inside wrapper)
- [ ] No mobile overflow (test 360px; tables → horizontal scroll container; charts responsive)
- [ ] Every page mobile-optimized; tap targets >=44px
- [ ] Compress images (WebP, width-capped, lazy below fold); custom real photo per page
- [ ] Fix broken buttons/links (crawl + manual click-through; empty `href` forbidden)
- [ ] Success messages (e.g. “Follow-up scheduled.”) + error messages (field-specific) on every mutation
- [ ] Remove placeholder/lorem text, unused nav items
- [ ] Logo → `/dashboard` (or `/`), phone → `tel:+91...`, email → `mailto:...`
- [ ] Fix copyright line: `© 2026 {LegalName}. All rights reserved.` + Privacy/Terms/Cookies/Refunds links
