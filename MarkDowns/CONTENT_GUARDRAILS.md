# CONTENT_GUARDRAILS — never-use list (enforced in review)

Site must NOT look vibrant. Reject PR if any appear.

## Banned
- Purple gradients (any gradient wash, esp. purple/blue hero)
- Fill-shift / sweeping buttons; use solid, flat, high-contrast buttons
- Fake reviews, fake metrics, fake hero text (“Trusted by 10,000+”), fake customer counters
- Emoji icons (use text or inline SVG, professional)
- Em-dashes in copy (use commas, colons, or periods)
- Over-the-top scroll animation, cursor animation / custom cursor, parallax hero
- AI slug photos (generic smiling headset stock), AI slug copy (“Unlock synergy, elevate journey”)
- Made-with-AI badge/tag (remove before launch)
- Unsupported themes/plugins, copyrighted images without license
- Placeholder/lorem text, unsupported claims

## Required tone
Plain, factual, business-like. Headings state what page does (“Customer Management – search, edit, assign.”). No hype adjectives. Every number sourced or removed. Every photo custom/licensed + compressed + alt text. Footer has real business details + legal links.

## Review gate
`grep -ri "lorem\|10,000\|trusted by\|made with" web/ server/` must be empty. Image audit: list source + license in `docs/image-licenses.csv`.
