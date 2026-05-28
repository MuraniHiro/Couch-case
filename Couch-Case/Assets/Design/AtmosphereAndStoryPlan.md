# Couch Case - Atmosphere and Story Plan

## Tone Rules

- Treat Lacey as the subject, not the spectacle.
- Put fear in helplessness, routine, neglect, distorted memory, and loss of agency.
- Keep explicit injury mostly offscreen. Use sound, lighting, camera pressure, blocked movement, and environmental change.
- Avoid making real suffering feel like a challenge room. The player should feel trapped by the situation, not entertained by harm.

## How To Make The Game Look Strong

Use a small, controlled art direction instead of trying to make everything realistic at once:

- Use low-detail PS1/early-2000s shapes with modern lighting, fog, film grain, and shadows.
- Make every room readable with one dominant practical light: fluorescent school lights, alley lamp, bedroom lamp, TV glow.
- Use dirty, repeated textures: old carpet, stained wallpaper, scratched desks, painted brick, dust, fabric weave.
- Use silhouettes for people during traumatic memories. Let the player's mind fill in the details.
- Animate unease through small changes: flickering lights, audio dropouts, slight camera sway, harder movement, longer interaction timers.
- Keep the color script restrained: school is green-gray and cold, alley is red-black, home is amber-brown at first, then sickly yellow and blue.
- Add post-processing early: vignette, film grain, subtle chromatic aberration, low exposure, fog, and narrow field of view during panic.

Free or affordable asset targets:

- Kenney prototype kits for blockout props.
- Poly Haven for public-domain textures and HDRIs.
- AmbientCG for public-domain PBR materials.
- Freesound for ambience, but verify licenses before shipping.
- Mixamo for placeholder humanoid animation.

## Twelve Couch-Year Scene Beats

1. Year 1: The routine collapses. The couch becomes the new bed. The player can still look around and call out, but getting up fails.
2. Year 2: Time skips through TV light. Parents enter with food, speak softly, and leave quickly. The room feels smaller.
3. Year 3: The player notices school papers, old objects, and previous life memories gathering dust out of reach.
4. Year 4: Movement is mostly limited to looking and small hand motions. The TV becomes the main window to the world.
5. Year 5: A medical-care dialogue appears: yes, refuse, silence. Any choice decays into silence before it reaches the room.
6. Year 6: The house soundscape changes. Footsteps, dishes, weather, and distant conversations become more important than visuals.
7. Year 7: Bugs and rot are suggested with sound, shadows, and close-up texture changes, not explicit gore.
8. Year 8: The parents' faces become harder to recognize. Their dialogue repeats old phrases in the wrong order.
9. Year 9: The TV begins answering thoughts. Hallucination and broadcast audio blend together.
10. Year 10: The room geometry subtly stretches. Doorways look far away. The couch feels like the center of the whole house.
11. Year 11: The parents are perceived as distorted figures. They still perform routine care, but the player can no longer read them as human.
12. Year 12: Sound thins out, light becomes flat, and interaction prompts disappear. The scene ends quietly, then cuts to the police perspective.

## First Playable Slice Goal

The current prototype covers the nightmare prologue:

- Public school classroom.
- Classmate asks Lacey to meet outside.
- Transition to hallway and dead-end alley.
- Offscreen traumatic beat conveyed through darkness, camera shake, and dialogue.
- Wake-up in bed with dialogue that frames it as a recurring dream.

Next implementation target: three-day home routine with movement and action time slowing down by day.

## Current Home Routine Prototype

- Day 1: get out of bed, eat breakfast, do school work, watch TV, use bathroom, go back to bed.
- Day 2: same routine, but movement and task timers are slower.
- Day 3: movement is much slower, task timers stretch further, and Lacey skips the bathroom after watching TV.
- Day 4: breakfast, school work, and TV repeat; while watching TV, Lacey falls asleep on the couch.
- Day 5 setup: the prototype now ends with Lacey waking on the couch and unable to stand, ready for the twelve couch-year scenes.

## Couch-Era Control Shift

- The couch becomes the fixed player position.
- Movement is disabled.
- The camera sits low on the couch, slightly tilted upward and right.
- The mouse position drives the camera within a limited view cone rather than controlling a free first-person look.
- Looking near the edge of the view increases vignette and chromatic blur.
- Clickable hotspots replace walking interactions: TV, food, hallway, window, and later clues for the optional good ending.

## Implemented Couch Scenes

Scene 1:

- Lacey wakes on the couch and realizes she is stuck.
- The player clicks to try getting up.
- A 10-second progress attempt reaches only 20%, then fails.
- The player clicks to call for help.
- Lacey can only say a normal-volume "help".
- Silence, dot pause, "It's so quiet", more silence.
- Parents enter from the right-side hallway/entrance area and say they are going on vacation again.
- The argument plays out: Lacey says she cannot get up, mother says to get off the couch if she wants to come.
- Parents turn on the TV and leave.
- The player clicks the TV to fall asleep.

Scene 2:

- Parents return from vacation.
- They ask how Lacey has been.
- Lacey can only mutter "ba-" instead of "bad".
- A silence pause plays.
- Mother brings food and places it on the table in front of the TV.
- The player clicks the food.
- Lacey thinks about how good it looks but cannot reach it.
- Thirty seconds pass toward night with boredom dialogue and an early scary hallway figure event.
- Mother returns, turns on the TV, says goodnight, and Lacey falls asleep.

Scene 3:

- The TV is still running while Lacey's parents watch it.
- The player clicks the parents to zoom in and hear Lacey question whether they understand she cannot move.
- After the camera pulls back, a breathing sound comes from behind the couch and Lacey tries to dismiss it.
- Ten seconds later the parents leave without saying anything.
- The player gets investigation time with hotspots for keys, pills, painting, part of the window, hallway, couch end, opened TV cabinet, and the TV.
- Clicking the TV ends investigation time and continues the scene.
- Inspecting the pills lets the player notice the lid. The lid flashes green until clicked, then flashes yellow and records the good-ending clue.
- A knock comes at the front door. The parents answer in the hallway, and the visitor is too distant to understand.
- The parents say, "No sir, everything is fine, thank you," making Lacey question what happened.
- Mother brings food again; Lacey can only look at it.
- The player clicks the TV again to sleep, ending at the Scene 4 placeholder.
