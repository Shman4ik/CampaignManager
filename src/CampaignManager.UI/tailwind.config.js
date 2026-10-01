/** @type {import('tailwindcss').Config} */
// Токены дизайн-системы 2.0 и выбор между Tailwind v3 и v4 — задача T1.6.
module.exports = {
  content: [
    // .cs — тоже: классы, собранные в коде, иначе попадают в сборку только случайно (AUDIT, «CSS и JS»).
    './**/*.{razor,html,cs}',
    // App.razor и статическая страница ошибки живут в сервере.
    '../CampaignManager.Server/Components/**/*.razor',
  ],
  theme: {
    extend: {},
  },
  plugins: [],
}
