/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    './**/*.{razor,html}',
    './Components/**/*.{razor,html}',
  ],
  theme: {
    extend: {
      colors: {
        // Primary color - Slate/graphite neutral tone
        primary: {
          50: '#F8FAFC',
          100: '#F1F5F9',
          200: '#E2E8F0',
          300: '#CBD5E1',
          400: '#94A3B8',
          500: '#64748B',
          600: '#475569',
          700: '#334155',
          800: '#1E293B',
          900: '#0F172A',
          950: '#020617',
        },
        // Secondary color - Warm stone/brown (subtle RPG accent)
        secondary: {
          50: '#FAFAF9',
          100: '#F5F5F4',
          200: '#E7E5E4',
          300: '#D6D3D1',
          400: '#A8A29E',
          500: '#78716C',
          600: '#57534E',
          700: '#44403C',
          800: '#292524',
          900: '#1C1917',
          950: '#0C0A09',
        },
        // Accent color - Muted steel blue (restrained highlights)
        accent: {
          50: '#F0F5FA',
          100: '#DAEAF5',
          200: '#B8D4EA',
          300: '#8FB9DC',
          400: '#6B9CC8',
          500: '#4B7FAF',
          600: '#3D6792',
          700: '#325375',
          800: '#284058',
          900: '#1E2F3E',
          950: '#0F1821',
        },
        // Status colors
        success: {
          50: '#EDF9F0',
          100: '#D2EDD9',
          200: '#A9DDB7',
          300: '#7CC993',
          400: '#4FB26C',
          500: '#2C9D49',
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
          500: '#D97706',
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
          500: '#C71D20',
          600: '#A8191B',
          700: '#891416',
          800: '#680F11',
          900: '#460A0B',
          950: '#250506',
        },
        info: {
          50: '#F0F5FA',
          100: '#DAEAF5',
          200: '#B8D4EA',
          300: '#8FB9DC',
          400: '#6B9CC8',
          500: '#4B7FAF',
          600: '#3D6792',
          700: '#325375',
          800: '#284058',
          900: '#1E2F3E',
          950: '#0F1821',
        }
      }
    }
  },
  plugins: [],
}

