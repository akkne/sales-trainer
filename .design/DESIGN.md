# Emerald Orbit Design System

### 1. Overview & Creative North Star
Creative North Star: The Playful Cartographer
Emerald Orbit is designed to transform structured learning into a tactile, exploratory journey. Unlike traditional rigid corporate dashboards, this system uses "Bouncy Brutalism"—a style that combines high-contrast structural lines with soft, pill-shaped elements and physics-based interactions. The goal is to make digital progress feel physical, using a zig-zagging "Path of Mastery" that breaks the standard vertical scroll.

### 2. Colors
Emerald Orbit utilizes a high-energy palette centered around growth and momentum.
- Primary (Growth Green): #58CC02. Used for active progress and success states.
- Secondary (Atmospheric Blue): #1CB0F6. Used for energy and XP tracking.
- Tertiary (Radiant Gold): #FFC800. Reserved for high-value achievements and streaks.
- Neutral Roles: Focuses on pure whites (#FFFFFF) and soft grays (#F7F7F7) to let the vibrant colors pop.

The "No-Line" Rule:
Avoid 1px borders for internal sectioning. Instead, use background color shifts (e.g., transitioning from surface to `surface_container_low`) or the "Tactile Drop" technique (a thick 4px-6px bottom shadow in a darker shade of the element's color) to define clickable boundaries.

Surface Hierarchy & Nesting:
- Level 0 (Background): Pure #FFFFFF.
- Level 1 (Section Containers): #F7F7F7 with rounded-xl (2rem) corners.
- Floating Elements: Use surface_bright with a soft shadow-lg for popovers and tooltips.

### 3. Typography
The typography scale is designed to be expressive and friendly, prioritizing readability in an educational context.
- Display & Headlines: Space Grotesk (replaces Fredoka). This provides a technical yet geometric playfulness. Sizes range from 3rem (48px) for hero titles to 1.5rem (24px) for section headers.
- Body & Interface: Lexend (replaces Nunito). A font designed specifically for reading proficiency. Standard body text is 1.125rem (18px).
- Labels & Stats: Space Grotesk Bold at 0.875rem (14px) for uppercase tracking-wider labels.

### 4. Elevation & Depth
Depth in Emerald Orbit is not about soft blurs alone; it is about Tactile Stacking.
- The Layering Principle: Use the primary-shadow (#58A700) or border-color (#E5E5E5) as a solid 4px-6px offset below buttons to create a "pushed" or "unpushed" state.
- Ambient Shadows: For floating popovers, use shadow-lg (0 10px 15px -3px rgba(0, 0, 0, 0.1)) to indicate a separate z-index layer.
- The "Bouncy" Interaction: All elevated elements must transition down 2px-4px on click, effectively "hiding" their bottom shadow to simulate physical compression.

### 5. Components
- Bouncy Buttons: High-saturation containers with a 4px bottom shadow of a darker tonal variant. No 1px border.
- Skill Nodes: Circular buttons (70x70px) with central icons. Active nodes use a "pulsing" outer ring (`primary/20`).
- Progress Paths: Thick (4px-10px) vertical or zig-zagging lines using outline for inactive and primary for completed segments.
- Status Widgets: Simple cards with 16px corner radius, utilizing iconography from the Material Symbols library with a "Fill" setting of 1.

### 6. Do's and Don'ts
Do:
- Use intentional asymmetry in layout (e.g., zig-zagging node placement).
- Use high-contrast color for progress (Green on White).
- Apply a 4px shadow to primary CTAs to make them feel "pressable."

Don't:
- Use 1px solid black or dark gray borders.
- Mix sharp 0px corners with the system's standard 16px/32px radius.
- Use low-contrast text on primary backgrounds; stick to pure white (#FFFFFF) for labels on colored buttons.