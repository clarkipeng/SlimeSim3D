# Taste

Read `~/.agentworld/lab/taste.md` first; this file refines it for this repo.

- The thumbnail stays the original grey slime: the DensityCircle look Clark shot (`Slime3D_Shot.webp` on the site). New looks never replace it.
- The site's three examples (grey density with dark veins, a violet line tangle, a teal line tangle) are the reference looks; clips and stills are judged against them side by side.
- Hover clips start from nothing and let the slime grow: an empty volume at frame 0, the full sphere by the end.
- Minimal camera motion: a locked camera, or at most one slow move. No zooms.
- Real time is the point. A look ships only when it runs live in the Unity project (60 fps at 1080p), not as an offline render.
- Show a live real-time capture on the wall before anything is final, and keep the item open until the PI has seen it.
- Real simulation only: every frame comes from the sim; never AI-generated or AI-edited.
- Try looks with LiveCapture's `-look` and `-sim` overrides; change Clark's tuned assets only when he asks.
