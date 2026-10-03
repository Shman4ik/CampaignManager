namespace CampaignManager.UI.Shared;

/// <summary>Вариант сегментированного переключателя (<c>Segmented</c>): значение, подпись и, если подпись коротка
/// («0», «+1»), полное имя для чтения с экрана.</summary>
public sealed record SegmentOption<TValue>(TValue Value, string Text, string? AriaLabel = null, string? TestId = null);
