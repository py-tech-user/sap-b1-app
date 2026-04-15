# Copilot Instructions

## General Guidelines
- When identifying the main root cause, state it immediately and directly, then apply the fix without waiting for extra prompting.
- Never write the word 'SAP' in user-facing text.

## Project-Specific Rules
- For invoice listing, user wants SQL-based reading preserved and does not want switching to Service Layer for /invoices. However, temporarily allow fallback to Service Layer for /invoices during SQL access issue resolution.
- For commercial documents, user wants SQL reading only (without fallback), while maintaining writes via Service Layer; partners remain in read/write via Service Layer.
- Do not remove background prefetch behavior for page 2 while the user is on page 1 in commercial list pages (including invoices).