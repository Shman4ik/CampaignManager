using System.ComponentModel.DataAnnotations;

namespace CampaignManager.Web.Model
{
    /// <summary>
    /// Класс для хранения данных персонажа в базе данных
    /// </summary>
    public class CharacterStorageDto
    {
        /// <summary>
        /// Уникальный идентификатор персонажа
        /// </summary>
        [Key]
        public Guid Id { get; set; }

        /// <summary>
        /// Имя персонажа, дублируется из JSON для быстрого доступа
        /// </summary>
        [StringLength(100)]
        public string CharacterName { get; set; }

        /// <summary>
        /// Профессия персонажа, дублируется из JSON для быстрого доступа
        /// </summary>
        [StringLength(100)]
        public string Occupation { get; set; }

        /// <summary>
        /// Имя игрока, дублируется из JSON для быстрого доступа
        /// </summary>
        [StringLength(100)]
        public string PlayerName { get; set; }

        /// <summary>
        /// Дата создания персонажа
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Дата последнего обновления персонажа
        /// </summary>
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// JSON-представление персонажа (тип JSONB в PostgreSQL)
        /// </summary>
        public string CharacterData { get; set; }
    }
}