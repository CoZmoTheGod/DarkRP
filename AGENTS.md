# DarkRP2 Agent Guidelines

This project is a fork of the official Facepunch Sandbox gamemode. Its goal is to recreate DarkRP for S&Box with a modular and maintainable architecture.

## Core Principles

- Build systems that are modular, clean, and easy to extend.
- Write readable, simple, and maintainable code.
- Follow best coding practices consistently.
- Stay direct and avoid unnecessary context or overengineering.
- Never trust the client. The server is authoritative.

## Implementation Rules

- Prefer small, focused components with clear responsibilities.
- Favor explicit behavior over clever abstractions.
- Keep game logic deterministic and easy to reason about.
- Validate client input on the server side.
- Put security, correctness, and maintainability before convenience.
