# Testing: User Avatars

> Scope: Phase 05r7 — avatar upload on the own profile page, GeoAvatar fallback.

## Manual Test Checklist

### Default / fallback state

- [ ] New user with no uploaded avatar sees the GeoAvatar (coloured SVG based on display name seed)
- [ ] GeoAvatar is circular on the profile page (88 px circle)
- [ ] GeoAvatar is shown when the uploaded image URL returns a 4xx/5xx error (onError fallback)

### Hover overlay

- [ ] On `/profile` (own profile), hovering the avatar reveals a semi-transparent dark circle overlay with the 📷 emoji
- [ ] Overlay disappears when the cursor moves away
- [ ] The public profile page `/friends/[userId]` does NOT show the 📷 overlay (read-only view)

### File picker

- [ ] Clicking the avatar (or the 📷 overlay) opens the native file picker
- [ ] File picker is restricted to `image/png`, `image/jpeg`, `image/webp` only
- [ ] Cancelling the file picker with no selection does nothing

### Upload flow

- [ ] Selecting a valid PNG/JPG/WebP file starts the upload immediately
- [ ] During upload the overlay shows a spinning indicator instead of 📷 and the cursor becomes a wait cursor
- [ ] After a successful upload the avatar on the page updates to the new image without a full page reload
- [ ] The new image URL contains a `?v=<n>` cache-buster so the browser fetches the fresh image even though the path is the same (`/avatars/{userId}`)

### Error handling

- [ ] Uploading a file that exceeds 5 MB returns an error from the backend
- [ ] The inline error message is displayed below the profile header (red text, matches the page's existing error style)
- [ ] After an error the upload button is re-enabled and the previous avatar is still shown

### Regression

- [ ] The rest of the profile page (stats, skills, achievements, theme switcher, logout) is unaffected
- [ ] Header profile chip avatar (top-right) updates after re-loading the page (profile query is invalidated on success)
- [ ] No console errors or React warnings during the upload interaction
