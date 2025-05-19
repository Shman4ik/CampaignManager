/** @type {import('tailwindcss').Config} */
module.exports = {
  content: ["./**/*.{razor,html,cshtml}"],
  theme: {
    extend: {
      colors: {
        // Primary color - Deep abyssal blue-green (Cthulhu's domain)
        primary: {
          50: '#E6F7F7',
          100: '#C0EAE9',
          200: '#93D9DA',
          300: '#64C8C8',
          400: '#3EAFB0',
          500: '#1A9697', // Base primary color
          600: '#137C7D',
          700: '#0D6364',
          800: '#074A4B',
          900: '#043233',
          950: '#021A1A',
        },
        // Secondary color - Aged parchment/manuscript tones
        secondary: {
          50: '#FCFAF6',
          100: '#F7F2E8',
          200: '#F0E6D1',
          300: '#E5D3B3',
          400: '#D4BA8C',
          500: '#C3A06C', // Base secondary color
          600: '#A88552',
          700: '#8C6D3F',
          800: '#71562F',
          900: '#574222',
          950: '#2B2113',
        },
        // Accent color - Eldritch purple (cosmic energy)
        accent: {
          50: '#F5EEFA',
          100: '#E9D5F4',
          200: '#D9B6ED',
          300: '#C28EE3',
          400: '#A95FD6',
          500: '#8F3BC7', // Base accent color
          600: '#752DB3',
          700: '#5C2090',
          800: '#45186D',
          900: '#30104A',
          950: '#1A0827',
        },
        // Status colors
        success: {
          50: '#EDF9F0',
          100: '#D2EDD9',
          200: '#A9DDB7',
          300: '#7CC993',
          400: '#4FB26C',
          500: '#2C9D49', // Base success color
          600: '#218239',
          700: '#17682B',
          800: '#104F1F',
          900: '#0A3614',
          950: '#051D0A',
        },
        warning: {
          50: '#FDF4E7',
          100: '#FBE3C2',
          200: '#F6CA8B',
          300: '#F2AE54',
          400: '#ED9220',
          500: '#D97706', // Base warning color - Ancient tome warning
          600: '#B36306',
          700: '#8C4E05',
          800: '#663904',
          900: '#402302',
          950: '#221301',
        },
        error: {
          50: '#FBECEC',
          100: '#F6D0D1',
          200: '#EEA7A8',
          300: '#E67779',
          400: '#DD4548',
          500: '#C71D20', // Base error color - Blood ritual red
          600: '#A8191B',
          700: '#891416',
          800: '#680F11',
          900: '#460A0B',
          950: '#250506',
        },
        info: {
          50: '#EEF3FB',
          100: '#D5E3F6',
          200: '#AECCEF',
          300: '#82AFE6',
          400: '#5690DB',
          500: '#2A6BC9', // Base info color
          600: '#2158AF',
          700: '#194492',
          800: '#112F70',
          900: '#0A1E4A',
          950: '#050F27',
        }
      }
    }
  },
  plugins: [],
}
