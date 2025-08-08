# PowerDocker

A simple, easy-to-use Docker container management tool built with .NET and Terminal.Gui, inspired by lazydocker but with better Docker Compose project grouping.

https://github.com/user-attachments/assets/b0f8bb18-f106-483e-9765-9d3e417015eb

## Elevator Pitch

* Docker Desktop is a slow and awkward experience. 
* Lazydocker can't organize containers into projects and focuses on unimportant things like logs, images, volumes, and network.
* **PowerDocker** is fast and extremely simple. Start or stop your containers or projects with one keystroke.

## Features

- **Simple Text-based UI**: Clean, keyboard-navigable interface 
- **Compose Project Grouping**: Automatically groups containers by their Docker Compose projects
- **Keyboard Shortcuts**: Fast control with simple key commands (r=restart, s=stop, e=exit)
- **Auto-refresh**: Real-time updates every 5 seconds with cursor position preservation
- **Standalone Container Support**: Shows non-compose containers under "Standalone" group

## Installation

### Prerequisites

1. Install .NET 8.0 or later
2. Install Docker
3. Configure Docker permissions (Linux only):
   ```bash
   sudo usermod -aG docker $USER
   # Log out and back in, or restart your session
   ```

### Install PowerDocker

#### Option 1: Clone and Build from Source

```bash
# Clone the repository
git clone https://github.com/MichalSkoula/powerdocker.git
cd powerdocker

# Build the application
dotnet build

# Run the application
dotnet run
```

#### Option 2: Build and Install Globally As Dotnet Tool (recommended)

```bash
# Clone and build
git clone https://github.com/MichalSkoula/powerdocker.git
cd powerdocker
dotnet build -c Release

# Install as global tool (optional)
dotnet pack -c Release
dotnet tool install --global --add-source ./bin/Release PowerDocker

# Run from anywhere
powerdocker
```

#### Option 3: Create Standalone Executable

```bash
# Clone the repository
git clone https://github.com/MichalSkoula/powerdocker.git
cd powerdocker

# Create self-contained executable
dotnet publish -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true -o ./publish

# Run the standalone executable
./publish/PowerDocker
```

Replace `linux-x64` with your platform:
- Windows: `win-x64`
- macOS: `osx-x64` or `osx-arm64` (Apple Silicon)
- Linux: `linux-x64` or `linux-arm64`
