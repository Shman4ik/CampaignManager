using CampaignManager.Web.Components.Features.Characters.Model;
using QuestPDF;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CampaignManager.Web.Utilities.Services;

public class PdfExportService
{
    static PdfExportService()
    {
        Settings.License = LicenseType.Community;
    }

    // Color palette (Lovecraftian green + sepia accents)
    private const string BgPanel = "#F7F2E6"; // light parchment
    private const string BorderDark = "#3A4A3C"; // deep green
    private const string Accent = "#7A5C2E"; // sepia accent
    private const string Muted = "#555555";

    /// <summary>
    /// Генерация PDF листа персонажа в стилистике CoC 7e
    /// </summary>
    public async Task<byte[]> GenerateCharacterPdfAsync(Character character)
    {
        return await Task.Run(() =>
        {
            var doc = Document.Create(pageContainer =>
            {
                pageContainer.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(25);
                    page.DefaultTextStyle(t => t.FontSize(9));

                    page.Header().Element(c => ComposeHeader(c, character));
                    page.Content().Element(c => ComposeBody(c, character));
                    page.Footer().AlignRight().Text(txt =>
                    {
                        txt.Span("Стр. ");
                        txt.CurrentPageNumber();
                        txt.Span(" / ");
                        txt.TotalPages();
                        txt.Span("  •  CampaignManager").FontColor(Muted).FontSize(8);
                    });
                });
            });

            return doc.GeneratePdf();
        });
    }

    #region Layout Composition

    private void ComposeHeader(IContainer container, Character c)
    {
        container.PaddingBottom(8).BorderBottom(1).BorderColor(BorderDark).Row(row =>
        {
            row.RelativeItem().Column(col =>
            {
                col.Item().Text(c.PersonalInfo?.Name.OrDash()).FontSize(18).Bold().FontColor(BorderDark);
                col.Item().Text(c.PersonalInfo?.Occupation.OrDash()).FontSize(11).FontColor(Accent);
            });
            row.RelativeItem().AlignRight().Column(col =>
            {
                col.Item().Text($"Игрок: {c.PersonalInfo?.PlayerName.OrDash()}").FontSize(9);
                col.Item().Text($"Дата: {DateTime.Now:dd.MM.yyyy}").FontSize(9);
            });
        });
    }

    private void ComposeBody(IContainer container, Character c)
    {
        container.Column(col =>
        {
            col.Spacing(10);

            // Top info band
            col.Item().Element(e => Panel(e, inner => inner.Row(r =>
            {
                r.RelativeItem().Element(x => ComposePersonalInfo(x, c));
                r.ConstantItem(8);
                r.RelativeItem().Element(x => ComposeDerivedAttributes(x, c));
            }), title: "Основная информация"));

            // Characteristics grid
            col.Item().Element(e => Panel(e, x => ComposeCharacteristicsGrid(x, c), title: "Характеристики"));

            // Skills (flowing columns)
            col.Item().Element(e => Panel(e, x => ComposeSkills(x, c), title: "Навыки"));

            // Weapons & Spells
            if (c.Weapons.Any() || c.Spells.Any())
                col.Item().Element(e => Panel(e, x => ComposeCombatMagicSection(x, c), title: "Сражение / Магия"));

            // Equipment & Finances
            if ((c.Equipment?.Items.Any() ?? false) || c.Finances is not null)
                col.Item().Element(e => Panel(e, x => ComposeEquipmentFinances(x, c), title: "Снаряжение и Финансы"));

            // Biography / Backstory / Notes
            if (HasBiography(c))
                col.Item().Element(e => Panel(e, x => ComposeBiography(x, c), title: "История и Заметки"));
        });
    }

    #endregion

    #region Section Implementations

    private void ComposePersonalInfo(IContainer container, Character c)
    {
        container.Column(col =>
        {
            col.Item().Text($"Имя: {c.PersonalInfo?.Name.OrDash()}");
            col.Item().Text($"Профессия: {c.PersonalInfo?.Occupation.OrDash()}");
            col.Item().Text($"Возраст: {c.PersonalInfo?.Age}");
            col.Item().Text($"Пол: {c.PersonalInfo?.Gender.OrDash()}");
            col.Item().Text($"Место рождения: {c.PersonalInfo?.Birthplace.OrDash()}");
            col.Item().Text($"Проживание: {c.PersonalInfo?.Residence.OrDash()}");
            col.Item().Text($"Телосложение: {c.PersonalInfo?.Build.OrDash()}");
            col.Item().Text($"Бонус урона: {c.PersonalInfo?.DamageBonus.OrDash()}");
            col.Item().Text($"Скорость: {c.PersonalInfo?.MoveSpeed}");
            col.Item().Text($"Уклонение: {c.PersonalInfo?.Dodge}");
        });
    }

    private void ComposeDerivedAttributes(IContainer container, Character c)
    {
        var d = c.DerivedAttributes;
        container.Column(col =>
        {
            Stat(col, "HP", d.HitPoints);
            Stat(col, "MP", d.MagicPoints);
            Stat(col, "SAN", d.Sanity);
            Stat(col, "Удача", d.Luck);
        });

        static void Stat(ColumnDescriptor col, string label, AttributeWithMaxValue v)
        {
            // AttributeWithMaxValue has properties Value and MaxValue (inferred from constructor signature new(0,0))
            var valueProp = v?.GetType().GetProperty("Value");
            var maxProp = v?.GetType().GetProperty("MaxValue");
            var currentVal = valueProp?.GetValue(v) ?? 0;
            var maxVal = maxProp?.GetValue(v) ?? 0;
            col.Item().Row(r =>
            {
                r.ConstantItem(40).Text(label + ":").SemiBold();
                r.RelativeItem().Text($"{currentVal}/{maxVal}");
            });
        }
    }

    private void ComposeCharacteristicsGrid(IContainer container, Character c)
    {
        var ch = c.Characteristics;
        var data = new (string Label, AttributeValue? Val)[]
        {
            ("Сила", ch.Strength), ("Телослож.", ch.Constitution), ("Размер", ch.Size),
            ("Ловкость", ch.Dexterity), ("Внешность", ch.Appearance), ("Интеллект", ch.Intelligence),
            ("Сила воли", ch.Power), ("Образован.", ch.Education)
        };

        container.Table(table =>
        {
            const int cols = 4;
            table.ColumnsDefinition(cd =>
            {
                for (int i = 0; i < cols; i++) cd.RelativeColumn();
            });

            int cellIndex = 0;
            foreach (var item in data)
            {
                table.Cell().Element(cell => AttributeCell(cell, item.Label, item.Val));
                cellIndex++;
            }
        });
    }

    private void AttributeCell(IContainer container, string label, AttributeValue? v)
    {
        container.Padding(4).Border(1).BorderColor(BorderDark).Background("#FFFFFF").Column(col =>
        {
            col.Item().Text(label).SemiBold().FontSize(9).FontColor(BorderDark);
            if (v is null)
            {
                col.Item().Text("—").FontColor(Muted);
            }
            else
            {
                col.Item().Text($"{v.Regular} ({v.Half}/{v.Fifth})").FontSize(9);
            }
        });
    }

    private void ComposeSkills(IContainer container, Character c)
    {
        var groups = c.Skills?.SkillGroups ?? new List<SkillGroup>();
        if (!groups.Any())
        {
            container.Text("Нет навыков").FontColor(Muted);
            return;
        }

        // Flow into two columns using a table for deterministic layout
        container.Table(table =>
        {
            const int cols = 2;
            table.ColumnsDefinition(cd => { for (int i = 0; i < cols; i++) cd.RelativeColumn(); });

            int col = 0;
            foreach (var g in groups)
            {
                table.Cell().Element(cell =>
                {
                    cell.Padding(4).Border(1).BorderColor("#DDD").Background("#FFFFFF").Column(cc =>
                    {
                        cc.Item().Text(g.Name).Bold().FontColor(Accent).FontSize(10);
                        foreach (var s in g.Skills)
                        {
                            var line = $"{s.Name}: {s.Value.Regular} ({s.Value.Half}/{s.Value.Fifth})";
                            cc.Item().Text(line).FontSize(8);
                        }
                    });
                });
                col = (col + 1) % cols;
            }
        });
    }

    private void ComposeCombatMagicSection(IContainer container, Character c)
    {
        container.Column(col =>
        {
            if (c.Weapons.Any())
            {
                col.Item().Column(section =>
                {
                    section.Item().Element(e => e.PaddingBottom(2).Text("Оружие").Bold().FontColor(Accent));
                    section.Item().Element(e => e.Table(table =>
                    {
                        table.ColumnsDefinition(cd =>
                        {
                            cd.RelativeColumn(2);
                            cd.RelativeColumn();
                            cd.RelativeColumn();
                            cd.RelativeColumn();
                            cd.RelativeColumn();
                            cd.RelativeColumn();
                            cd.RelativeColumn();
                        });

                        table.Header(header =>
                        {
                            header.Cell().Background(BorderDark).Padding(2).AlignCenter().Text("Название").FontColor("#FFFFFF").SemiBold().FontSize(8);
                            header.Cell().Background(BorderDark).Padding(2).AlignCenter().Text("Навык").FontColor("#FFFFFF").SemiBold().FontSize(8);
                            header.Cell().Background(BorderDark).Padding(2).AlignCenter().Text("Урон").FontColor("#FFFFFF").SemiBold().FontSize(8);
                            header.Cell().Background(BorderDark).Padding(2).AlignCenter().Text("Дальность").FontColor("#FFFFFF").SemiBold().FontSize(8);
                            header.Cell().Background(BorderDark).Padding(2).AlignCenter().Text("Атаки").FontColor("#FFFFFF").SemiBold().FontSize(8);
                            header.Cell().Background(BorderDark).Padding(2).AlignCenter().Text("Боепр.").FontColor("#FFFFFF").SemiBold().FontSize(8);
                            header.Cell().Background(BorderDark).Padding(2).AlignCenter().Text("Осечка").FontColor("#FFFFFF").SemiBold().FontSize(8);
                        });

                        foreach (var w in c.Weapons)
                        {
                            BodyCell(table, w.Name);
                            BodyCell(table, w.Skill);
                            BodyCell(table, w.Damage);
                            BodyCell(table, w.Range);
                            BodyCell(table, w.Attacks);
                            BodyCell(table, w.Ammo);
                            BodyCell(table, w.Malfunction);
                        }
                    }));
                });
            }

            if (c.Spells.Any())
            {
                col.Item().Element(_ => _.Height(6));
                col.Item().Column(section =>
                {
                    section.Item().Element(e => e.PaddingBottom(2).Text("Заклинания").Bold().FontColor(Accent));
                    section.Item().Element(e => e.Table(table =>
                    {
                        table.ColumnsDefinition(cd =>
                        {
                            cd.RelativeColumn(2);
                            cd.RelativeColumn();
                            cd.RelativeColumn();
                            cd.RelativeColumn();
                            cd.RelativeColumn(3);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Background(BorderDark).Padding(2).AlignCenter().Text("Название").FontColor("#FFFFFF").SemiBold().FontSize(8);
                            header.Cell().Background(BorderDark).Padding(2).AlignCenter().Text("Стоимость").FontColor("#FFFFFF").SemiBold().FontSize(8);
                            header.Cell().Background(BorderDark).Padding(2).AlignCenter().Text("Время").FontColor("#FFFFFF").SemiBold().FontSize(8);
                            header.Cell().Background(BorderDark).Padding(2).AlignCenter().Text("Тип").FontColor("#FFFFFF").SemiBold().FontSize(8);
                            header.Cell().Background(BorderDark).Padding(2).AlignCenter().Text("Описание").FontColor("#FFFFFF").SemiBold().FontSize(8);
                        });

                        foreach (var s in c.Spells)
                        {
                            BodyCell(table, s.Name);
                            BodyCell(table, s.Cost.OrDash());
                            BodyCell(table, s.CastingTime.OrDash());
                            BodyCell(table, s.SpellType);
                            BodyCell(table, Truncate(s.Description, 160));
                        }
                    }));
                });
            }
        });
    }

    private void ComposeEquipmentFinances(IContainer container, Character c)
    {
        container.Row(row =>
        {
            row.RelativeItem().Element(e =>
            {
                e.Column(col =>
                {
                    col.Item().Text("Снаряжение").Bold().FontColor(Accent);
                    if (!(c.Equipment?.Items.Any() ?? false))
                        col.Item().Text("—").FontColor(Muted);
                    else
                        foreach (var item in c.Equipment!.Items)
                            col.Item().Text($"• {item.Name}: {item.Description}").FontSize(8);
                });
            });
            row.RelativeItem().Element(e =>
            {
                e.Column(col =>
                {
                    col.Item().Text("Финансы").Bold().FontColor(Accent);
                    var f = c.Finances;
                    if (f is null)
                    {
                        col.Item().Text("—").FontColor(Muted);
                    }
                    else
                    {
                        col.Item().Text($"Наличные: {f.Cash.OrDash()}");
                        col.Item().Text($"Карманные: {f.PocketMoney.OrDash()}");
                        if (f.Assets.Any())
                        {
                            col.Item().Text("Активы:").SemiBold();
                            foreach (var a in f.Assets) col.Item().Text("• " + a).FontSize(8);
                        }
                    }
                });
            });
        });
    }

    private void ComposeBiography(IContainer container, Character c)
    {
        container.Column(col =>
        {
            void AddBlock(string title, string? text)
            {
                if (string.IsNullOrWhiteSpace(text)) return;
                col.Item().Element(e => e.PaddingTop(2).Text(title).SemiBold().FontColor(Accent));
                col.Item().Text(text).FontSize(8);
            }

            AddBlock("Внешность", c.Biography?.Appearance);
            AddBlock("Черты", c.Biography?.Traits);
            AddBlock("Идеалы и Принципы", c.Biography?.IdealsAndPrinciples);
            AddBlock("Предыстория", c.Backstory);
            if (!string.IsNullOrWhiteSpace(c.Notes)) AddBlock("Заметки", c.Notes);
        });
    }

    #endregion

    #region Helpers

    private bool HasBiography(Character c) =>
        !string.IsNullOrWhiteSpace(c.Biography?.Appearance) ||
        !string.IsNullOrWhiteSpace(c.Biography?.Traits) ||
        !string.IsNullOrWhiteSpace(c.Biography?.IdealsAndPrinciples) ||
        !string.IsNullOrWhiteSpace(c.Backstory) ||
        !string.IsNullOrWhiteSpace(c.Notes);

    private void Panel(IContainer outer, Action<IContainer> content, string title)
    {
        outer.Element(o =>
        {
            o.Background(BgPanel).Border(1).BorderColor(BorderDark).Padding(6).Column(col =>
            {
                col.Spacing(4);
                col.Item().Text(title).Bold().FontColor(BorderDark).FontSize(11);
                col.Item().Element(content);
            });
        });
    }

    // Removed HeaderCell helper (inlined for simplicity)

    private static void BodyCell(TableDescriptor table, string? text)
    {
        table.Cell().Padding(2).Text(text.OrDash()).FontSize(8);
    }

    private static string Truncate(string value, int len)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return value.Length <= len ? value : value.Substring(0, len - 1) + "…";
    }

    #endregion
}

internal static class PdfExportExtensions
{
    public static string OrDash(this string? value) => string.IsNullOrWhiteSpace(value) ? "—" : value!;
}