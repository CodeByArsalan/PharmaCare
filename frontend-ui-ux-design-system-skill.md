# Frontend UI/UX Design System Skill File (Bootstrap)

## Purpose
This skill file defines a general-purpose UI/UX design system for building web application frontends with **Bootstrap 5**. It is not tied to any specific app or page — it covers reusable design elements, interaction states, and usability patterns that apply to any web app frontend. Use it as the foundation before designing app-specific pages.

---

## 1. Design Foundations

### 1.1 Layout & Grid
- Build all pages on Bootstrap's 12-column responsive grid (`container`, `row`, `col-*`). Use `container-fluid` for data-dense screens and `container` for centered, content-focused pages.
- Establish a consistent spacing scale using Bootstrap spacing utilities (`m-*`, `p-*`, `gap-*`) — pick multiples of 4px/8px and avoid arbitrary custom margins.
- Group related content in `card` components with clear visual separation (border, subtle shadow, or background tint) rather than relying on whitespace alone.
- Keep a consistent page structure: header/navbar → optional page title/breadcrumb → main content area → footer (if applicable). Don't vary this skeleton between pages.
- Use whitespace deliberately — dense areas (tables, forms) need tighter spacing; decision points (CTAs, empty states) need more breathing room to draw the eye.

### 1.2 Color System
- Define colors through Bootstrap Sass variables (`$primary`, `$secondary`, `$success`, `$warning`, `$danger`, `$info`, `$light`, `$dark`) — never hardcode hex values inline in components.
- Limit the palette: one primary brand color, one accent, and the standard semantic set (success/warning/danger/info). More than this dilutes visual hierarchy.
- Reserve semantic colors for their meaning only: green = success/positive, red = error/destructive, amber = caution, blue = informational/neutral action. Don't reuse "danger red" for a decorative element.
- Maintain WCAG AA contrast (minimum 4.5:1 for normal text, 3:1 for large text/icons) between foreground and background colors.
- Never use color as the only signal — pair it with text, icons, or patterns for accessibility (color blindness affects ~8% of men).
- Support light backgrounds as default; if dark mode is in scope, define it as a parallel token set, not inverted colors patched ad hoc.

### 1.3 Typography
- Use one primary font family across the entire app (system font stack or one imported web font). A second family may be used only for headings, not both interchangeably.
- Define a clear type scale, e.g.:
  - H1/Page title: 28–32px, bold
  - H2/Section header: 20–24px, semibold
  - H3/Card header: 16–18px, semibold
  - Body text: 14–16px, regular
  - Caption/helper text: 12–13px, regular, muted color
- Maintain consistent line-height (1.4–1.6 for body text) for readability.
- Use font-weight to establish hierarchy, not font-size changes alone — avoid more than 3 weight variants (regular, semibold, bold) across the app.
- Left-align body text and long-form content; right-align or tabular-align numeric data in tables for scannability.

### 1.4 Iconography & Imagery
- Use a single icon library throughout (e.g., Bootstrap Icons) — mixing icon sets creates visual inconsistency in stroke width and style.
- Icons paired with text labels are preferred over icon-only buttons, especially for primary actions. Icon-only buttons must have a tooltip and `aria-label`.
- Keep icon sizing consistent relative to adjacent text (typically 1–1.25x the line-height of the label).
- Images/thumbnails should use consistent aspect ratios and object-fit rules across all instances of the same component type (e.g., all avatar images are circular and 32px).

---

## 2. Core UI Components & States

### 2.1 Buttons
- **Hierarchy**: One primary button per view/section (the main call-to-action). Use `btn-primary` for it; secondary/tertiary actions use `btn-outline-secondary` or `btn-link`.
- **Sizing**: Use consistent button sizes (`btn-sm`, default, `btn-lg`) based on context — don't mix sizes within the same action group.
- **States**: Every button must have a visually distinct default, hover, active/pressed, focus (visible outline/ring), disabled, and loading state (spinner + disabled) defined.
- **Destructive actions**: Use `btn-danger` and require a confirmation step for irreversible actions (delete, remove, cancel with data loss).
- **Labeling**: Buttons should use action-oriented verbs ("Save Changes," "Delete Item") rather than vague labels ("OK," "Submit") wherever the action has specific consequences.
- **Touch targets**: Minimum 40–44px tap height on touch devices; adequate spacing (at least 8px) between adjacent buttons to prevent mis-taps.

### 2.2 Forms & Inputs
- Label every input explicitly (`<label for>`) — never rely on placeholder text as the only label, since it disappears on input and fails accessibility.
- Group related fields visually (e.g., `fieldset` or card sections) and use a single-column layout for forms wherever possible — it's faster to scan and complete than multi-column layouts.
- Mark required fields consistently (asterisk + legend, or "(optional)" tags on non-required fields — pick one convention and use it everywhere).
- Validate inline, on blur or submit — not on every keystroke (this feels punishing). Show success indication (green check/border) once a field is corrected.
- Error messages should be specific and actionable ("Email must include an @ symbol," not "Invalid input") and appear directly below the relevant field, with `aria-live` for screen readers.
- Use appropriate input types (`type="email"`, `type="number"`, `type="date"`) to trigger correct mobile keyboards and native validation.
- Disable the submit button (or show a spinner) during submission to prevent duplicate submits.

### 2.3 Cards & Content Containers
- Use consistent internal padding and header/body/footer structure across all cards in the app.
- Keep card titles short and scannable; use secondary text for metadata (dates, tags, counts).
- If cards are clickable/interactive, indicate this with a hover elevation/border change and cursor pointer — don't make an entire card silently clickable without visual affordance.

### 2.4 Tables
- Use sticky table headers for long tables so column context isn't lost while scrolling.
- Right-align numeric columns, left-align text columns.
- Provide sortable column headers with a clear sort-direction indicator (arrow icon).
- Include empty-state and loading-state variants for every table (see Section 4).
- Support pagination or infinite scroll for large datasets — never render unbounded rows at once.

### 2.5 Navigation Components
- **Navbar**: Fixed or sticky top bar containing branding, primary navigation or search, and user/account menu.
- **Sidebar**: Used for apps with many sections; collapsible on smaller viewports into an off-canvas drawer. Highlight the active route clearly (background tint + bold label + optional left border accent).
- **Tabs**: Use for switching between views of the same object/context (e.g., "Details / Activity / Settings" on one record). Don't use tabs for unrelated top-level navigation.
- **Breadcrumbs**: Use on any page nested more than one level deep, so users always have a path back without relying on browser back.

### 2.6 Modals & Overlays
- Reserve modals for focused, short tasks (confirmations, quick forms) — avoid multi-step wizards or long forms inside modals.
- Always provide a clear close affordance (X icon, Cancel button, and Esc key support) and trap focus within the modal while open.
- Dim the background (backdrop) to signal modality and prevent interaction with underlying content.
- Confirmation modals for destructive actions should restate what will be affected ("This will permanently delete 'Product A'") rather than a generic "Are you sure?"

### 2.7 Notifications & Alerts
- Use **toasts** for transient, non-blocking feedback (e.g., "Changes saved") — auto-dismiss after 3–5 seconds, positioned consistently (commonly top-right).
- Use **inline alerts** (`alert-success`, `alert-danger`, etc.) for persistent, page-level messages that need to remain visible until dismissed or resolved.
- Never use a blocking modal for simple success confirmations — that adds unnecessary friction.
- Keep alert copy concise and actionable; include a next-step link/button when relevant ("Undo," "View Details").

---

## 3. UX Principles

### 3.1 Navigation Flow
- Keep primary navigation shallow (2 levels max) so users can reach any core feature within 2 clicks/taps.
- Maintain a consistent, predictable location for global actions (search, notifications, account menu) across every page.
- Preserve user context when navigating back (e.g., filters/scroll position retained when returning from a detail page to a list).

### 3.2 Accessibility (a11y)
- Support full keyboard operability: logical tab order, visible focus indicators, and no keyboard traps.
- Use semantic HTML elements (`button`, `nav`, `header`, `main`, `table`) instead of generic `div`/`span` with attached click handlers.
- Provide alt text for meaningful images; mark purely decorative images as `alt=""`.
- Ensure form errors, loading states, and dynamic content updates are announced via ARIA live regions for screen reader users.
- Test with browser zoom up to 200% and verify no content is clipped or overlapping.

### 3.3 Interaction Feedback
- Every user-triggered action needs a visible response within ~100ms — even if it's just a state change (button pressed appearance) before the full result loads.
- Use skeleton loaders for content that takes longer than ~300ms to load, rather than blank space or a lone spinner.
- Confirm successful actions with unobtrusive feedback (toast, checkmark animation, inline state change) — don't require the user to guess whether an action succeeded.
- Clearly differentiate error feedback (color, icon, and message) from informational feedback.

### 3.4 Usability Patterns
- **Progressive disclosure**: Show only essential information/actions by default; reveal advanced options behind "Show more," accordions, or secondary views. Avoid overwhelming first-time users.
- **Consistency over novelty**: Reuse the same interaction pattern for the same type of action everywhere in the app (e.g., all "delete" actions look and behave the same way).
- **Forgiving interactions**: Prefer undo over confirmation dialogs for reversible actions ("Item deleted — Undo") to reduce friction while still protecting against mistakes.
- **Predictable defaults**: Pre-fill sensible defaults in forms and settings to reduce user effort, but always allow easy overriding.

### 3.5 Empty, Loading, and Error States
Every data-driven view (list, table, dashboard widget) should define all four states explicitly:
1. **Loading state** — skeleton placeholders matching the eventual content's shape.
2. **Empty state** — friendly message, brief explanation, and a clear call-to-action to populate data (not just "No data found").
3. **Error state** — explain what went wrong in plain language and provide a retry action.
4. **Populated state** — the standard, fully-loaded view.

---

## 4. Responsiveness & Cross-Device Consistency

- Design mobile-first; verify layouts at Bootstrap's breakpoints: `sm` (≥576px), `md` (≥768px), `lg` (≥992px), `xl` (≥1200px).
- Collapse multi-column layouts into single-column stacks below `md`; convert horizontal navigation into an off-canvas or hamburger menu below `lg`.
- Convert wide data tables into card-per-row layouts on small screens rather than allowing horizontal scroll as the default experience.
- Ensure touch targets are at least 44x44px and spaced adequately apart on touch devices.
- Test critical flows (forms, checkout-like flows, navigation) on at least one physical or emulated mobile device, not just a resized desktop browser window.

---

## 5. Performance & Technical Consistency

- Load only the Bootstrap components/utilities actually used; avoid shipping the full framework bundle unnecessarily.
- Lazy-load below-the-fold images and non-critical scripts.
- Debounce expensive operations triggered by user input (search-as-you-type, live filtering) by ~250–300ms.
- Paginate or virtualize long lists/tables instead of rendering all rows at once.
- Maintain consistent behavior across major browsers (Chrome, Firefox, Safari, Edge) — pay particular attention to flexbox/grid rendering differences and native form control styling in Safari.
- Use a single shared stylesheet/theme (Bootstrap Sass variables + one custom override file) rather than page-specific inline styles, to guarantee visual consistency as the app grows.

---

## 6. Recommended Tools & Technologies

| Purpose | Recommended Tool |
|---|---|
| CSS Framework | Bootstrap 5 (customized via Sass variables) |
| Icons | Bootstrap Icons or Font Awesome |
| Form Validation | Bootstrap validation classes + a JS validation library for complex rules |
| Data Tables | DataTables.js or a lightweight custom table with server-side pagination |
| Charts | Chart.js or ApexCharts |
| Build Tooling | Vite or Webpack for bundling/minifying custom JS and Sass |
| Accessibility Testing | axe DevTools or Lighthouse accessibility audit |
| Cross-Browser Testing | BrowserStack or manual testing across Chrome/Firefox/Safari/Edge |
| E2E Testing | Cypress or Playwright |

---

## 7. Quick Design Review Checklist
- [ ] Does every button have defined hover, active, focus, disabled, and loading states?
- [ ] Is there exactly one primary CTA per view/section?
- [ ] Are colors used semantically and paired with text/icons, not color alone?
- [ ] Is typography consistent with the defined type scale across all pages?
- [ ] Does every data view define loading, empty, error, and populated states?
- [ ] Are all interactive elements keyboard-accessible with visible focus indicators?
- [ ] Has the layout been tested at `sm`, `md`, `lg`, and `xl` breakpoints?
- [ ] Do destructive actions require confirmation or offer undo?
- [ ] Is feedback (toast/alert/inline) used consistently for success and error cases app-wide?
- [ ] Has the page been checked in at least 3 major browsers?
