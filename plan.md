# Campaign Manager - Scenario Management Feature Implementation Plan

## 1. Overview

This document outlines the implementation plan for adding scenario management features to the Campaign Manager application, allowing Game Masters to create scenario templates and add them to campaigns.

## 2. Data Models

### 2.1 Scenario
- `Scenario`: Base model for adventure scenarios
  - Properties: Id, Name, Description, Location, Era, Journal (Markdown)
  - Navigation: Collection of NPCs, Creatures, Items
  - Flags: IsTemplate, CreatorEmail, CampaignId (nullable)

### 2.2 Characters and NPCs
- Reuse existing `Character` model with `CharacterType.NonPlayerCharacter`
- Create `ScenarioNpc`: Simplified model for NPCs in scenarios
  - Properties: Id, Name, Description, Role, Location, Notes, CharacterId (nullable)
  - Navigation: Scenario

### 2.3 Creatures
- `Creature`: Model for monsters and supernatural entities
  - Properties: Id, Name, Type, Description, Stats (simplified), Abilities, ImageUrl (optional)
  - Navigation: Categories, Scenarios (many-to-many)

### 2.4 Items and Artifacts
- `Item`: Model for significant items and artifacts
  - Properties: Id, Name, Type, Description, Effects, Rarity, ImageUrl (optional)
  - Navigation: Categories, Scenarios (many-to-many)

## 3. Database Schema Changes

### 3.1 Tables
- `Scenarios`: Store scenario data
- `ScenarioNpcs`: Store NPCs associated with scenarios
- `Creatures`: Store creature/monster data
- `Items`: Store item/artifact data
- `ScenarioCreatures`: Junction table for scenario-creature relationships
- `ScenarioItems`: Junction table for scenario-item relationships

### 3.2 Relationships
- `Campaign` ← 1:Many → `Scenario` (optional)
- `Scenario` ← 1:Many → `ScenarioNpc`
- `Scenario` ← Many:Many → `Creature`
- `Scenario` ← Many:Many → `Item`
- `Character` ← 1:1 → `ScenarioNpc` (optional)

## 4. Services Implementation

### 4.1 ScenarioService
- CRUD operations for scenarios
- Methods to clone templates to campaigns
- Journal management functionality

### 4.2 CreatureService
- CRUD operations for creatures
- Search and filtering capabilities
- Category management

### 4.3 ItemService
- CRUD operations for items/artifacts
- Search and filtering capabilities
- Category management

## 5. UI Components

### 5.1 Page Components
- `ScenariosPage.razor`: List and manage scenario templates
- `ScenarioDetailPage.razor`: Create/edit scenario details
- `CreaturesPage.razor`: Browse/manage creature database
- `CreatureDetailPage.razor`: Create/edit creature details
- `ItemsPage.razor`: Browse/manage item database
- `ItemDetailPage.razor`: Create/edit item details
- `CampaignScenariosPage.razor`: Manage scenarios within a campaign

### 5.2 Reusable Components
- `JournalEditor.razor`: Markdown editor for scenario journals
- `ScenarioNpcList.razor`: List and manage NPCs in a scenario
- `CreatureList.razor`: List creatures with filtering
- `ItemList.razor`: List items with filtering
- `LocationFilter.razor`: Filter NPCs/creatures/items by location

## 6. Navigation and Menu Updates

- Add "Scenarios" section to the main navigation
- Add "Creatures" and "Items" sections under the Wiki menu
- Add "Scenarios" tab to Campaign management interface

## 7. Integration Features

### 7.1 Campaign Integration
- Add ability to create scenarios from templates
- Add campaign scenario management UI
- Link NPCs to existing character system

### 7.2 Image Management
- Implement image upload functionality
- Consider AI image generation integration
- Define image storage strategy

## 8. Implementation Phases

### Phase 1: Core Models and Database
- Implement data models
- Create database migrations
- Update database context
- Implement basic services

### Phase 2: Scenario Management
- Create scenario management pages
- Implement CRUD operations
- Create journal editor component
- Update navigation menu

### Phase 3: Creatures and Items
- Implement creature database features
- Implement items database features
- Create filter components

### Phase 4: Campaign Integration
- Implement template-to-campaign scenario creation
- Integrate with existing campaign system
- Add campaign scenario management UI

### Phase 5: Polish and Enhancements
- Implement image management
- Add location filtering
- UI polish and responsiveness improvements
- Performance optimizations

## 9. Technical Considerations

### 9.1 Markdown Editor
- Consider using Blazorise Markdown Editor
- Alternative: Integrate Markdig for Markdown rendering

### 9.2 Image Management
- Use Blazor InputFile for uploads
- Consider cloud storage for images (Azure Blob Storage)
- Implement basic image resizing/optimization

### 9.3 Performance
- Implement pagination for large collections
- Add caching for frequently accessed data
- Consider lazy loading for images

## 10. Testing Strategy

- Unit tests for service methods
- Component tests for UI components
- Integration tests for database operations
- End-to-end tests for key user workflows