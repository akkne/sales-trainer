# Testing: Edit Profile modal

> Scope: "Edit profile" button on `/profile` now opens a modal (same pattern as
> "Manage skills") for editing **name**, **position (persona)**, and **photo** in
> one place. Backend: `PUT /profile` (name + optional persona), reusing the existing
> `POST /avatars` for the photo.

## Manual Test Checklist

### Opening / closing
- [ ] On `/profile`, clicking **Edit profile** opens a centered modal titled "Edit profile"
- [ ] The modal shows the current avatar, current name pre-filled, and current position pre-selected
- [ ] Clicking the ✕ button, the "Cancel" button, the overlay backdrop, or pressing `Esc` closes the modal without saving
- [ ] Re-opening after a cancel shows the original (unsaved) values again

### Name
- [ ] The name field is pre-filled with the current display name and focused on open
- [ ] Clearing the name (or entering only spaces) disables the "Save" button
- [ ] Name is capped at 100 characters (input `maxLength`)

### Position (persona)
- [ ] The position dropdown lists: Not set, SDR, Account Executive, Account Manager, Founder, Other
- [ ] The current persona is pre-selected; "Not set" is selected when the user has no persona
- [ ] Selecting "Not set" leaves the persona unchanged on the server (name-only update)

### Photo
- [ ] "Change photo" opens the native file picker restricted to PNG/JPG/WebP
- [ ] Selecting a valid image uploads immediately; a spinner shows over the avatar during upload
- [ ] After a successful upload the modal avatar and the page avatar both update (cache-busted `?v=<n>`)
- [ ] A file over 5 MB shows an inline error under the "Change photo" button; the avatar is unchanged

### Save
- [ ] Clicking "Save" with a valid name persists name (and persona if chosen); the modal closes
- [ ] The profile identity row updates (name + persona · email) without a full page reload
- [ ] The header profile chip (top-right) reflects the new name after the profile query is invalidated
- [ ] If the save fails, an inline "Couldn't save" message appears and the modal stays open

### Backend / propagation
- [ ] `PUT /profile` with an empty/whitespace `displayName` → `400`
- [ ] `PUT /profile` with `displayName` > 100 chars → `400`
- [ ] `PUT /profile` with an invalid `persona` value → `400`
- [ ] A successful `PUT /profile` publishes `UserUpdatedEvent`; other services' user replicas (ai, notification) show the new display name

### Regression
- [ ] "Manage skills" modal still opens/closes independently and is unaffected
- [ ] No console errors or React warnings during the edit interaction
