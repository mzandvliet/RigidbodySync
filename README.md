# RigidbodySync
A prototype for Volo Airsport multiplayer networking code, done in 2016 with Frank Versnel (@fversnel).

It's built on top of Lidgren, though we tried UNET low level transport before that. Some features:

- Network roles as separate components. It is set up such that no component has to do any branching to find the code path for its current combination of client/server/owner/listener setup.
- Specifically built to handle this case: players control fast moving objects (50-75m/s) and fly parallel. If player A looks left, they should see player B and vice versa. This is actually very tricky to get right.
- A dual simulation (forward Euler) is used to correct individual rigidbody motion - NAT punchthrough
