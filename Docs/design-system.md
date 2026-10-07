# Design System

EZ Across Control uses a semantic Design System that strictly prohibits the usage of hardcoded HEX colors in UI components. All UI elements must use the defined semantic tokens.

## Typography
The standard font for the entire application is **Inter**.

## Themes
The application supports three themes:
- Light
- Dark
- Follow System (Default)

## Color Palette

### Light Mode
- Background: `#F6F8FC`
- Surface: `#FFFFFF`
- Surface Soft: `#EEF2F8`
- Border: `#E2E8F2`
- Text Primary: `#102347`
- Text Secondary: `#70809B`
- Brand Coral: `#FF604B`
- Brand Coral Soft/Hover: `#FFF0ED`
- Success: `#16835B`
- Shadow: `rgba(30,55,90,0.09)`
- Shadow Soft: `rgba(30,55,90,0.08)`

### Dark Mode
- Background: `#08162C`
- Surface: `#102543`
- Surface Soft: `#152D4D`
- Border: `#244062`
- Text Primary: `#EDF4FF`
- Text Secondary: `#9FB0C9`
- Brand Coral: `#FF604B`
- Brand Coral Soft/Hover: `#3B2833`
- Success: `#16835B`
- Shadow: `rgba(0,0,0,0.28)`
- Shadow Soft: `rgba(0,0,0,0.22)`

## Semantic States
Components must map these basic HEX values into semantic resources/tokens:
- **SuccessPrimary**: Uses `Success`
- **SuccessBackground**: Can be derived or mapped to surface soft, depending on the need.
- **WarningPrimary**, **WarningBackground**, **ErrorPrimary**, **ErrorBackground**, **InfoPrimary**, **InfoBackground**, **Neutral**.

*Note: The coral and success colors preserve their identity across Light and Dark themes, while structural tokens (Background, Surface, Border) change.*
