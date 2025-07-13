# JustDanceSnatcher Utilities

This project is a collection of various command-line tools, originally created to handle specific and sometimes random asset management tasks for *Just Dance*. It has grown into a suite of functions for downloading, upgrading, and modifying various asset files by interacting with Discord bots and local file systems.

---

## 🚨 IMPORTANT DISCLAIMER 🚨

**This project is intended strictly for educational and research purposes only.** By using this software, you acknowledge and agree to the following terms and assume all associated risks.

### 1. **Violation of Discord's Terms of Service**
This tool utilizes **user account automation** ("self-botting") to send commands to Discord. This is a **direct violation of Discord's Terms of Service and Developer Policies**. Using this tool can, and likely will, result in the **permanent suspension or termination** of the Discord account associated with the provided bot token. The developers of this tool are not responsible for any action taken against your account.

### 2. **Risk of Copyright Infringement & Legal Action**
The primary function of this tool is to download game assets (videos, audio, images, etc.) that are the **copyrighted intellectual property of Ubisoft**.
- Unauthorized downloading, sharing, or modification of these assets constitutes copyright infringement and is illegal in most countries.
- Distributing a tool designed to circumvent access controls and acquire copyrighted material may expose you to legal action from the rights holder (Ubisoft), including **DMCA takedown notices** against your GitHub repository and potential lawsuits.

### 3. **No Affiliation**
This project is **not affiliated with, endorsed by, or sponsored by Discord or Ubisoft**. All trademarks, service marks, and company names are the property of their respective owners.

### 4. **Use At Your Own Risk**
You, the user, are solely responsible for your actions. Any damage, account termination, or legal consequences resulting from the use of this software are your own responsibility. The creators of this project provide it as-is, without warranty, and will not be held liable for any misuse.

---

## Features

- **JDNext Downloader**: Downloads complete song assets for *Just Dance Next* via a Discord bot.
- **Server Video Upgrader**: Upgrades existing video files for songs with higher quality versions.
- **UbiArt Video Upgrader**: Upgrades video files for older UbiArt-based *Just Dance* titles.
- **ContentAuthorization JSON Tools**: Utilities to create and download assets from `contentAuthorization.json` files.
- **Custom Audio Volume Fixer**: Uses FFmpeg to analyze and normalize the volume of custom audio tracks.
- **Playlist Converter**: Converts playlists from one format to another (Next to Next+).
- **Manual Cover Extractor Helper**: Assists in organizing manually extracted cover art.
- **Tag Fixer**: Automatically cleans up specific tags in `SongInfo.json` files.

## Prerequisites

- **.NET 8 SDK** (or a compatible .NET runtime).
- **FFmpeg**: Required for the "Fix Custom Audio Volume" feature. It must be installed and accessible in your system's PATH.
- **A Discord Bot Token**: You must have a bot token to use any of the Discord-related features.

## Setup & Installation

1.  **Clone the repository:**
    ```bash
    git clone [your-repository-url]
    cd JustDanceSnatcher
    ```

2.  **Create the Bot Token File:**
    In the root directory of the project, create a file named `Secret.txt` and paste your Discord bot token into it. The file should contain only the token string.

3.  **Restore Dependencies & Build:**
    Open a terminal in the project's root directory and run:
    ```bash
    dotnet restore
    dotnet build
    ```

## How to Run

1.  Run the application from your terminal:
    ```bash
    dotnet run
    ```

2.  A menu will appear with the available operations. Type the number corresponding to the tool you wish to use and press `Enter`.

3.  Follow the on-screen prompts provided by the selected tool. For Discord-based tools, ensure Discord is your active window to allow for automated command input.
