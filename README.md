# dots_netcode_relay_sample
A simplistic sample to have dots netcode work with relay and lobby


<img width="416" alt="image" src="https://github.com/Occuros/dots_netcode_relay_sample/assets/66752261/5fb18e11-df2e-4be0-b52b-4980f901cdb7">


A short explanation of the main elements:

The game starts in a local client/server simulation mode. In this mode, the lobby is loaded with the following parts:

- The big purple cube allows hosting a game, simply move into the big cube to start hosting a game.
- Yellow spheres represent existing Lobby rooms which can be joined when moving into them.
- Dark ground plates are open lobby spots

Once connected (through the relay server) the real game starts with a blue floor and the same player cubes.


Movement is possible with both `WASD` and `arrow keys`
