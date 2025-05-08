using System;
using System.IO;
using System.Threading.Tasks;
using CampaignManager.Web.Model;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Collections.Generic;
using System.Linq;

namespace CampaignManager.Web.Services
{
    public class PdfExportService
    {
        // Register QuestPDF license (using community edition)
        static PdfExportService()
        {
            QuestPDF.Settings.License = LicenseType.Community;
        }

        /// <summary>
        /// Generates a PDF document from a character
        /// </summary>
        /// <param name="character">The character to export</param>
        /// <returns>Byte array containing the PDF document</returns>
        public async Task<byte[]> GenerateCharacterPdfAsync(Character character)
        {
            return await Task.Run(() =>
            {
                var document = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(20);
                        page.DefaultTextStyle(x => x.FontSize(10));

                        page.Header().Element(ComposeHeader);
                        
                        page.Content().Element(contentContainer =>
                        {
                            ComposeContent(contentContainer, character);
                        });

                        page.Footer().AlignCenter().Text(text =>
                        {
                            text.Span("Страница ").FontSize(10);
                            text.CurrentPageNumber().FontSize(10);
                            text.Span(" из ").FontSize(10);
                            text.TotalPages().FontSize(10);
                        });
                    });

                    void ComposeHeader(IContainer container)
                    {
                        container.Row(row =>
                        {
                            row.RelativeItem().Column(column =>
                            {
                                column.Item().Text($"{character.PersonalInfo?.Name ?? "Персонаж"}")
                                      .Bold().FontSize(16);
                                column.Item().Text("Персонаж")
                                      .FontSize(12);
                            });

                            row.ConstantItem(80).Text(DateTime.Now.ToString("dd.MM.yyyy"))
                                .FontSize(10).Italic();
                        });
                    }
                });

                return document.GeneratePdf();
            });
        }

        private void ComposeContent(IContainer container, Character character)
        {
            container.Column(column =>
            {
                // Personal section
                column.Item().Element(e => ComposePersonalSection(e, character));
                column.Item().Height(15);

                // Characteristics section
                column.Item().Element(e => ComposeCharacteristicsSection(e, character));
                column.Item().Height(15);

                // Skills section
                column.Item().Element(e => ComposeSkillsSection(e, character));
                column.Item().Height(15);

                // Combat section
                column.Item().Element(e => ComposeCombatSection(e, character));
                column.Item().Height(15);

                // Equipment section
                column.Item().Element(e => ComposeEquipmentSection(e, character));
                column.Item().Height(15);

                // Biography section (if any)
                if (!string.IsNullOrEmpty(character.Biography?.Appearance) || 
                    !string.IsNullOrEmpty(character.Biography?.Traits) ||
                    !string.IsNullOrEmpty(character.Biography?.IdealsAndPrinciples))
                {
                    column.Item().Element(e => ComposeBiographySection(e, character));
                }
            });
        }

        private void ComposePersonalSection(IContainer container, Character character)
        {
            container.Column(column =>
            {
                column.Item().Text("Личные данные").Bold().FontSize(14);
                column.Item().BorderBottom(1).BorderColor(Colors.Grey.Medium).PaddingBottom(5);
                column.Item().PaddingTop(5);

                column.Item().Grid(grid =>
                {
                    grid.Columns(3);
                    grid.Item(2).Column(col =>
                    {
                        col.Item().Text($"Имя: {character.PersonalInfo?.Name ?? "—"}");
                        col.Item().Text($"Происхождение: {character.PersonalInfo?.Birthplace ?? "—"}");
                        col.Item().Text($"Возраст: {character.PersonalInfo?.Age ?? 0}");
                        col.Item().Text($"Пол: {character.PersonalInfo?.Gender ?? "—"}");
                    });
                });
            });
        }

        private void ComposeCharacteristicsSection(IContainer container, Character character)
        {
            container.Column(column =>
            {
                column.Item().Text("Характеристики").Bold().FontSize(14);
                column.Item().BorderBottom(1).BorderColor(Colors.Grey.Medium).PaddingBottom(5);
                column.Item().PaddingTop(5);

                column.Item().Grid(grid =>
                {
                    grid.Columns(3);
                    
                    // First column - main characteristics
                    grid.Item().Column(col =>
                    {
                        col.Item().Text("Характеристики:").Bold();
                        if (character.Characteristics != null)
                        {
                            col.Item().Text($"Сила: {character.Characteristics.Strength.Regular}");
                            col.Item().Text($"Ловкость: {character.Characteristics.Dexterity.Regular}");
                            col.Item().Text($"Интеллект: {character.Characteristics.Intelligence.Regular}");
                            col.Item().Text($"Телосложение: {character.Characteristics.Constitution.Regular}");
                        }
                    });
                    
                    // Second column - more characteristics
                    grid.Item().Column(col =>
                    {
                        col.Item().Text("Дополнительно:").Bold();
                        if (character.Characteristics != null)
                        {
                            col.Item().Text($"Внешность: {character.Characteristics.Appearance.Regular}");
                            col.Item().Text($"Сила воли: {character.Characteristics.Power.Regular}");
                            col.Item().Text($"Размер: {character.Characteristics.Size.Regular}");
                            col.Item().Text($"Образование: {character.Characteristics.Education.Regular}");
                        }
                    });
                    
                    // Third column - state
                    grid.Item().Column(col =>
                    {
                        col.Item().Text("Состояние:").Bold();
                        if (character.State != null)
                        {
                            col.Item().Text($"Без сознания: {(character.State.IsUnconscious ? "Да" : "Нет")}");
                            col.Item().Text($"Серьезная рана: {(character.State.HasSeriousInjury ? "Да" : "Нет")}");
                            col.Item().Text($"При смерти: {(character.State.IsDying ? "Да" : "Нет")}");
                        }
                    });
                });
            });
        }

        private void ComposeSkillsSection(IContainer container, Character character)
        {
            container.Column(column =>
            {
                column.Item().Text("Навыки").Bold().FontSize(14);
                column.Item().BorderBottom(1).BorderColor(Colors.Grey.Medium).PaddingBottom(5);
                column.Item().PaddingTop(5);

                // Create a grid layout for skills
                column.Item().Grid(grid =>
                {
                    grid.Columns(4); 
                    grid.Spacing(5); 
                    
                    // Distribute skill groups evenly across columns
                    var skillGroups = character.Skills?.SkillGroups ?? new List<SkillGroup>();
                    for (int i = 0; i < skillGroups.Count; i++)
                    {
                        var group = skillGroups[i];
                        grid.Item().Column(col =>
                        {
                            col.Spacing(2); 
                            col.Item().Text(group.Name).Bold().FontSize(10);
                            foreach (var skill in group.Skills)
                            {
                                col.Item().Text($"{skill.Name}: {skill.Value.Regular}").FontSize(9);
                            }
                        });
                    }
                });
            });
        }

        private void ComposeCombatSection(IContainer container, Character character)
        {
            container.Column(column =>
            {
                column.Item().Text("Оружие и заклинания").Bold().FontSize(14);
                column.Item().BorderBottom(1).BorderColor(Colors.Grey.Medium).PaddingBottom(5);
                column.Item().PaddingTop(5);

                // Weapons section
                if (character.Weapons != null && character.Weapons.Any())
                {
                    column.Item().Text("Оружие:").SemiBold();
                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(3);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Text("Название").SemiBold();
                            header.Cell().Text("Урон").SemiBold();
                            header.Cell().Text("Скилл").SemiBold();
                            header.Cell().Text("Вес").SemiBold();
                        });

                        foreach (var weapon in character.Weapons)
                        {
                            table.Cell().Text(weapon.Name);
                            table.Cell().Text(weapon.Damage);
                            table.Cell().Text(weapon.Skill);
                        }
                    });
                    column.Item().Height(10);
                }

                // Spells section
                if (character.Spells != null && character.Spells.Any())
                {
                    column.Item().Text("Заклинания:").SemiBold();
                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(3);
                            columns.RelativeColumn(3);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Text("Название").SemiBold();
                            header.Cell().Text("Описание").SemiBold();
                        });

                        foreach (var spell in character.Spells)
                        {
                            table.Cell().Text(spell.Name);
                            table.Cell().Text(spell.Description);
                        }
                    });
                }
            });
        }

        private void ComposeEquipmentSection(IContainer container, Character character)
        {
            container.Column(column =>
            {
                column.Item().Text("Снаряжение").Bold().FontSize(14);
                column.Item().BorderBottom(1).BorderColor(Colors.Grey.Medium).PaddingBottom(5);
                column.Item().PaddingTop(5);

                // Equipment section
                if (character.Equipment != null && character.Equipment.Items.Any())
                {
                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(3);
                            columns.RelativeColumn(3);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Text("Название").SemiBold();
                            header.Cell().Text("Описание").SemiBold();
                        });

                        foreach (var item in character.Equipment.Items)
                        {
                            table.Cell().Text(item.Name);
                            table.Cell().Text(item.Description);
                        }
                    });
                }
                else
                {
                    column.Item().Text("Снаряжение отсутствует");
                }
            });
        }

        private void ComposeBiographySection(IContainer container, Character character)
        {
            container.Column(column =>
            {
                column.Item().Text("Биография").Bold().FontSize(14);
                column.Item().BorderBottom(1).BorderColor(Colors.Grey.Medium).PaddingBottom(5);
                column.Item().PaddingTop(5);

                if (!string.IsNullOrEmpty(character.Biography?.Appearance))
                {
                    column.Item().Text("Внешность:").SemiBold();
                    column.Item().Text(character.Biography.Appearance);
                    column.Item().Height(5);
                }

                if (!string.IsNullOrEmpty(character.Biography?.Traits))
                {
                    column.Item().Text("Черты характера:").SemiBold();
                    column.Item().Text(character.Biography.Traits);
                    column.Item().Height(5);
                }

                if (!string.IsNullOrEmpty(character.Biography?.IdealsAndPrinciples))
                {
                    column.Item().Text("Идеалы и принципы:").SemiBold();
                    column.Item().Text(character.Biography.IdealsAndPrinciples);
                    column.Item().Height(5);
                }

                if (!string.IsNullOrEmpty(character.Backstory))
                {
                    column.Item().Text("Предыстория:").SemiBold();
                    column.Item().Text(character.Backstory);
                }
            });
        }
    }
}
