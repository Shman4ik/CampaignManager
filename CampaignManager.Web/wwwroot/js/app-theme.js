window.appTheme = {
  colorOptions: {
    primary: "#1A9697",    // Deep abyssal blue-green
    secondary: "#C3A06C",  // Aged parchment/manuscript
    success: "#2C9D49",    // Mystical green
    danger: "#C71D20",     // Blood ritual red
    warning: "#D97706",    // Ancient tome warning
    info: "#2A6BC9",       // Arcane blue
    light: "#F7F2E8",      // Faded parchment
    dark: "#2B2113",       // Ancient darkness
    accent: "#8F3BC7"      // Eldritch purple
  },
  darkMode: {
    enabled: true // Включаем тёмный режим, характерный для темы Ктулху
  }
};

// Function to apply theme colors to CSS variables
window.applyThemeColors = function() {
  const theme = window.appTheme;
  
  // Set CSS variables for the theme colors
  document.documentElement.style.setProperty('--color-primary', theme.colorOptions.primary);
  document.documentElement.style.setProperty('--color-secondary', theme.colorOptions.secondary);
  document.documentElement.style.setProperty('--color-success', theme.colorOptions.success);
  document.documentElement.style.setProperty('--color-danger', theme.colorOptions.danger);
  document.documentElement.style.setProperty('--color-warning', theme.colorOptions.warning);
  document.documentElement.style.setProperty('--color-info', theme.colorOptions.info);
  document.documentElement.style.setProperty('--color-light', theme.colorOptions.light);
  document.documentElement.style.setProperty('--color-dark', theme.colorOptions.dark);
  document.documentElement.style.setProperty('--color-accent', theme.colorOptions.accent);
  
  // Set dark mode
  if (theme.darkMode.enabled) {
    document.documentElement.classList.add('dark-mode');
  } else {
    document.documentElement.classList.remove('dark-mode');
  }
};

// Apply theme when DOM content is loaded
document.addEventListener('DOMContentLoaded', function() {
  window.applyThemeColors();
});
