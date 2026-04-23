# The Beams of Light

This is a technical demo and environment showcase I put together to figure out how to do proper 3D pixel art in Unity. It's basically a culmination of everything I've learned about getting some unqiue effects in unity3D URP.

It started out as a bit of a game idea, but I ended up focusing way more on the tech and the environment design. It's now more of a portfolio piece for my rendering and design work.

### What I've done:
I built a custom camera system (heavily inspired by t3ssel8r's methods) that snaps the view to a texel grid. It basically forces the pixels to stay locked in place relative to the internal resolution, so movement feels solid. 

I also had to do a lot of work on the URP pipeline itself. I wrote some custom Render Features to handle
**Internal Downscaling:** Rendering the world at a low resolution while keeping the UI and effects sharp.

I also spent a lot of time on the modularity of the "Beams of Light" environment. One thing I learned: you can't snap every object to the grid if you're using modular pieces, or you'll get tiny gaps (tearing) between things. Instead, I keep the objects at their float positions and only snap the camera. It’s a subtle fix but it made a massive difference in the final look.
Basically, this was a massive learning experience in extending URP and fighting with math to get a very specific retro-modern aesthetic.

---
Got a lot of inspiration from t3ssel8r for this. Built it in Unity 2022.2+ with URP. Also massive props to https://www.davidhol.land/articles/3d-pixel-art-rendering/ for outlining how he did it!
