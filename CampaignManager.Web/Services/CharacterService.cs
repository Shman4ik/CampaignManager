﻿using CampaignManager.Web.Companies.Models;
using CampaignManager.Web.Model;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CampaignManager.Web.Services;

public class CharacterService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IHttpContextAccessor httpContextAccessor,
    ILogger<CharacterService> logger)
{
    public async Task<Character> CreateCharacterAsync(Character character, Guid campaignPlayerId)
    {
        try
        {
            string userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
            {
                throw new UnauthorizedAccessException("User must be authenticated to create a character");
            }

            // Если ID не установлен, генерируем новый
            if (character.Id == Guid.Empty)
            {
                character.Id = Guid.CreateVersion7();
            }

            // Создаем DTO для хранения с дублированием ключевых полей
            CharacterStorageDto storageDto = new()
            {
                CharacterName = character.PersonalInfo.Name,
                CreatedAt = DateTime.UtcNow,
                LastUpdated = DateTime.UtcNow,
                Character = character,
                CampaignPlayerId = campaignPlayerId,
            };

            // Находим и деактивируем все активные персонажи этого игрока в этой кампании
            using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync();
            var existingActiveCharacters = await dbContext.CharacterStorage
                .Where(c => c.CampaignPlayerId == campaignPlayerId && c.Status == CharacterStatus.Active)
                .ToListAsync();

            foreach (var existingChar in existingActiveCharacters)
            {
                existingChar.Status = CharacterStatus.Inactive;
                existingChar.LastUpdated = DateTime.UtcNow;
                dbContext.Update(existingChar);
            }

            // Устанавливаем новый персонаж как активный
            storageDto.Status = CharacterStatus.Active;

            // Инициализируем базовые поля сущности
            storageDto.Init();
            storageDto.Id = character.Id;

            // Добавляем в базу данных
            dbContext.CharacterStorage.Add(storageDto);
            await dbContext.SaveChangesAsync();

            logger.LogInformation($"Character {character.Id} created by user {userId}");
            return character;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Error creating character: {ex.Message}");
            throw;
        }
    }

    public async Task<CharacterStorageDto?> GetCharacterByIdAsync(Guid id)
    {
        await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync();
        return await dbContext.CharacterStorage.FindAsync(id);
    }

    public async Task<CampaignPlayer?> GetCampaignPlayerAsync(Guid id)
    {
        await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync();
        return await dbContext.CampaignPlayers.FindAsync(id);
    }

    public async Task UpdateCharacterAsync(Character character)
    {
        try
        {
            string userEmail = GetCurrentUserId(); // Теперь возвращает email
            logger.LogInformation($"Attempting to update character {character.Id} by user email {userEmail}");

            if (string.IsNullOrEmpty(userEmail))
            {
                throw new UnauthorizedAccessException("User must be authenticated to update a character");
            }

            using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync();


            // Получаем существующую запись
            CharacterStorageDto? storageDto = await dbContext.CharacterStorage.FindAsync(character.Id);
            if (storageDto == null)
            {
                logger.LogWarning($"Character with ID {character.Id} not found during update");
                throw new KeyNotFoundException($"Character with ID {character.Id} not found");
            }

            storageDto.LastUpdated = DateTime.UtcNow;
            storageDto.Character = character;
            storageDto.CharacterName = character.PersonalInfo.Name;
            dbContext.Update(storageDto);
            await dbContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Error updating character: {ex.Message}");
            throw;
        }
    }

    public async Task SetCharacterStatusAsync(Guid characterId, CharacterStatus newStatus)
    {
        try
        {
            string userEmail = GetCurrentUserId();
            if (string.IsNullOrEmpty(userEmail))
            {
                throw new UnauthorizedAccessException("User must be authenticated to change character status");
            }

            using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync();
            
            // Получаем персонажа
            var character = await dbContext.CharacterStorage
                .Include(c => c.CampaignPlayer)
                .FirstOrDefaultAsync(c => c.Id == characterId);
                
            if (character == null)
            {
                throw new KeyNotFoundException($"Character with ID {characterId} not found");
            }

            // Если устанавливаем статус Active, деактивируем остальных персонажей этого игрока в этой кампании
            if (newStatus == CharacterStatus.Active)
            {
                var otherActiveCharacters = await dbContext.CharacterStorage
                    .Where(c => c.CampaignPlayerId == character.CampaignPlayerId 
                             && c.Status == CharacterStatus.Active
                             && c.Id != characterId)
                    .ToListAsync();

                foreach (var otherChar in otherActiveCharacters)
                {
                    otherChar.Status = CharacterStatus.Inactive;
                    otherChar.LastUpdated = DateTime.UtcNow;
                    dbContext.Update(otherChar);
                }
            }

            // Обновляем статус указанного персонажа
            character.Status = newStatus;
            character.LastUpdated = DateTime.UtcNow;
            dbContext.Update(character);
            
            await dbContext.SaveChangesAsync();
            logger.LogInformation($"Character {characterId} status changed to {newStatus} by user {userEmail}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Error changing character status: {ex.Message}");
            throw;
        }
    }

    private string? GetCurrentUserId()
    {
        return httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.Email)?.Value;
    }
}