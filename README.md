# KleeStore
![image](https://github.com/user-attachments/assets/f74036af-b756-44a1-a382-6e45dd29782e)
![image](https://github.com/user-attachments/assets/8d8aa245-3105-49e6-84f1-810c6cac12fd)

Let's cut the crap. Managing software on Windows can be a drag. You've got installers from a dozen different places, half of them trying to sneak in extra junk. Then there's Chocolatey – a godsend for us command-line warriors, taming the chaos one `choco install` at a time. But let's face it, not everyone wants to live in a terminal.

That's where KleeStore, this C# incarnation, barges in. This isn't some corporate, sanitized app store. This is **my personal project, a lean MVP (Minimum Viable Product)**, built because I wanted a slick, no-nonsense GUI for Chocolatey. Something that gets the job done without the usual Windows song and dance.

The mission? Simple. Take the raw power of Chocolatey and give it a face. A clean, modern interface that lets you browse, search, install, and ditch Chocolatey packages without ever *needing* to type `choco -y`. It's about making Chocolatey accessible, maybe even a little bit fun.

---

## Why This KleeStore? Why Bother?

Look, if you're already a Choco-pro, you might not *need* this. But if you've ever wished Chocolatey had a decent GUI, or if you're trying to get less terminal-savvy friends onto the Choco-train, then KleeStore might just be your new best mate. I'm building this to **cut through the noise**, offering a straightforward way to tap into Chocolatey's massive software arsenal. It's about **easy discovery** of useful apps and **centralized control** over what Chocolatey has put on your system. This C# version is my current weapon of choice for this fight – an MVP, sure, but it packs the essentials to make your package management life a bit less of a grind.

---

## What This C# Rebel Can Do (The Core Features)

KleeStore, in its current C# form, isn't trying to be everything to everyone. It's focused. You can **cruise through the Chocolatey community's package ocean**, or if you know what you're after, the **search bar will hunt it down**. Each package shows you the vital stats: version, what the heck it does (description), and sometimes how many other brave souls have downloaded it.

When you're ready to pull the trigger, **installing and uninstalling are just a click away**. No cryptic commands, no second-guessing. And to see the army of apps Chocolatey is already commanding on your rig, there's a dedicated **"Installed Packages" view**. Behind the scenes, KleeStore talks to the KleeStore API to **keep its package list fresh**, and it's smart enough to **cache package data and images** so it doesn't have to fetch everything, every single time. The UI itself? I'm aiming for **clean and modern**, built with WPF, something that doesn't look like it crawled out of Windows XP.

---

## The Bare Necessities (System Requirements)

To get KleeStore to join your rebellion, you'll need:

*   **Windows 10 or a newer model.**
*   The **.NET 7.0 Runtime** (or newer, if I've bumped the target).
*   **Chocolatey itself.** If you're a Choco-virgin, KleeStore will try to hold your hand through the installation.
*   **Admin rights.** Chocolatey needs to be the boss to install or rip out software. KleeStore will bug you (or Windows UAC will) when it's time to escalate.

---

## Get It On Your Machine (Installation)

Ready to give it a whirl?

1.  Snag the latest ZIP package from the [**Releases**](https://github.com/kleeedolinux/kleestores/releases) page right here on GitHub.
2.  Unzip that bad boy into any folder you damn well please.
3.  Fire up `KleeStore.exe`.

If KleeStore sniffs out that Chocolatey is missing in action on your system, it'll offer to help get it installed. No excuses.

---

## For the Code Slingers (Building from Source)

Want to peek under the hood or tinker with the C# guts?

**You'll need:**

*   **Visual Studio 2022** (or your .NET IDE of choice that can handle it).
*   The **.NET 7.0 SDK** (or whatever the project's currently targeting).

**How to wrestle it into submission:**

1.  Clone this repo:
    ```bash
    git clone https://github.com/kleeedolinux/kleestores.git
    ```
2.  Crack open the `KleeStore.csproj` (or `KleeStore.sln`) in Visual Studio.
3.  Tell Visual Studio to build the damn thing (Ctrl+Shift+B usually does the trick).
4.  Hit F5 and watch it (hopefully) spring to life.

---

## Making KleeStore Dance (Usage)

It's not rocket science.

The **"Browse" tab** is your gateway to Choco-land. Scroll, search, explore. Pagination at the bottom stops your scroll wheel from crying. Found something you want? Smash that **"Install" button**. KleeStore will handle the admin prompts.

Got regrets? Or just cleaning house? The **"Installed" tab** shows your current Choco-managed hoard. The **"Uninstall" button** is your friend there.

Want the freshest list of what's out there? The **"Refresh" button** pings the KleeStore API. Your "Installed" list also usually updates to show what's *really* on your machine.

---

## The Fine Print (License)

This whole shebang is under the **MIT License**. Go read the `LICENSE` file if you care about the legal mumbo-jumbo. Basically, use it, break it, fix it – just don't sue me.

---

## Hat Tips & Tools of the Trade

This project stands on the shoulders of giants (and some handy libraries):

*   **[Chocolatey](https://chocolatey.org/):** The undisputed king. KleeStore is just its humble servant.
*   **[KleeStore API](https://kleestoreapi.vercel.app/):** The backend that powers the package discovery and metadata.
*   **[WPF](https://docs.microsoft.com/en-us/dotnet/desktop/wpf/):** For crafting a modern, responsive UI that doesn't suck.
*   **[System.Text.Json](https://docs.microsoft.com/en-us/dotnet/api/system.text.json):** For parsing API responses without breaking a sweat.

---

**KleeStore (C# Edition): My shot at making Chocolatey a bit more rock 'n' roll for the GUI crowd. Give it a spin. Maybe you'll dig it.**
