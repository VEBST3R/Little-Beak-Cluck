# Little Beak Cluck

A 2D action game where you play as a brave little chicken defending against waves of enemies using the power of your voice!

## About

Little Beak Cluck is a wave-based combat game built in Unity. You control a chicken who can unleash devastating vocal attacks by charging up and letting out powerful screams. The longer you charge, the more waves you release - but timing is everything!

## Core Mechanics

### Voice Wave Combat
- **Charge System**: Hold to charge your attack (min 0.5s, max 3s)
- **Multi-Wave Attacks**: 
  - Short charge = 1 wave
  - Medium charge = 2 waves  
  - Full charge = 3 waves
- **Wave Types**: Switch between High, Mid, and Low frequency attacks (each with different properties)

### Game Modes
- **Campaign Mode**: Progress through designed waves with increasing difficulty. Your health fully restores between waves.
- **Endless Mode**: Survive as long as you can! Procedurally generated enemy waves and partial healing during cooldowns add to the challenge.

## Features

- Clean architecture with service locator pattern
- Object pooling for performance (using Lean Pool)
- Enemy HUD system with health bars and off-screen indicators
- Upgrade system for player progression
- Audio system with wave-type specific sounds
- Parallax backgrounds
- Input system supporting multiple control schemes

## Tech Stack

- **Engine**: Unity 2022+
- **Architecture**: Service-based with dependency injection
- **Pooling**: Lean Pool for optimization
- **Input**: New Unity Input System
- **UI**: Unity UI with custom components

## Code Highlights

- Service-based architecture (`ServiceLocator`, `IGameService`)
- Event-driven combat system
- Clean separation between game logic and presentation
- Proper cleanup and memory management (no memory leaks!)
- Robust null-checking throughout

## Project Structure

```
Assets/
├── Scripts/
│   ├── Audio/          # Audio management
│   ├── Combat/         # Attack system, damage, voice waves
│   ├── Enemies/        # Enemy behaviors and AI
│   ├── Player/         # Player controller, health, attack
│   ├── World/          # Wave manager, spawn points
│   ├── UI/             # All UI components
│   ├── Infrastructure/ # Core systems, services
│   └── Services/       # Game services
```

## State

This project is portfolio-ready. The codebase has been reviewed and critical issues have been addressed:
- All null reference checks in place
- Memory leaks fixed
- Event subscriptions properly cleaned up
- Singleton patterns implemented correctly

## Notes

Built as a portfolio piece showcasing:
- Clean code practices
- Unity development skills
- Game architecture design
- Performance optimization
- UI/UX implementation

---

*Made with Unity 🎮*
