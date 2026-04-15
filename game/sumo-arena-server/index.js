const express = require('express');
const http = require('http');
const { Server } = require('socket.io');

const app = express();
const server = http.createServer(app);
const io = new Server(server);

app.use(express.static('public'));

// --------------------- ÉTAT GLOBAL ---------------------

let gamePhase = 'LOBBY'; // 'LOBBY', 'PLAYING'

let players = {};              // id -> { id, h, v, push, state }
let socketsByPlayerId = {};    // id -> socket
let nextPlayerId = 1;

// Ensemble des participants de la manche en cours
let currentRoundPlayerIds = new Set();

// Fantômes
let maxGhostsActive = 0;
const ghostChance = 0.5; // 50% de chance d'être choisi fantôme
/*const ghostChance = 1.0; // 100% de chance pour tester */
let nextTrapId = 1;
let pendingTraps = [];   // { id, playerId, type }

// --------- Utilitaires ---------

function computeMaxGhosts(n) {
  if (n <= 0) return 0;
  if (n <= 4) return 1;
  if (n <= 10) return 2;
  if (n <= 20) return 3;
  return 4; 
  /* return n; */
}

function getAliveCount() {
  let c = 0;
  for (const id of currentRoundPlayerIds) {
    const p = players[id];
    if (p && p.state === 'alive') c++;
  }
  return c;
}

function getGhostCount() {
  let c = 0;
  for (const id of currentRoundPlayerIds) {
    const p = players[id];
    if (p && p.state === 'ghost') c++;
  }
  return c;
}

// --------------------- SOCKET.IO ---------------------

io.on('connection', (socket) => {
  const playerId = nextPlayerId++;

  players[playerId] = {
    id: playerId,
    h: 0,
    v: 0,
    push: false,
    state: 'idle' // pas encore dans la manche
  };

  socketsByPlayerId[playerId] = socket;
  socket.playerId = playerId;

  console.log(`Nouvelle connexion : joueur #${playerId} (socket ${socket.id})`);

  // info initiale pour la manette
  socket.emit('hello', {
    playerId,
    phase: gamePhase
  });

  io.emit('playerCount', Object.keys(players).length);

  // Inputs de la manette (utiles seulement si le joueur est alive)
  socket.on('input', (data) => {
    const p = players[playerId];
    if (!p) return;

    p.h = Number(data.h) || 0;
    p.v = Number(data.v) || 0;
    p.push = !!data.push;
  });

  // Pièges déclenchés par les fantômes
  socket.on('trap', (data) => {
    const p = players[playerId];
    if (!p || p.state !== 'ghost') return;

    const type = (data && data.type) || 'explosion';

    pendingTraps.push({
      id: nextTrapId++,
      playerId,
      type
    });

    console.log(`Trap ${type} déclenché par le fantôme #${playerId}`);
  });

  socket.on('disconnect', () => {
    console.log(`Déconnexion joueur #${playerId} (socket ${socket.id})`);

    delete players[playerId];
    delete socketsByPlayerId[playerId];
    currentRoundPlayerIds.delete(playerId);

    io.emit('playerCount', Object.keys(players).length);
  });
});

// --------------------- ROUTES HTTP ---------------------

app.get('/play', (req, res) => {
  res.sendFile(__dirname + '/public/play.html');
});

// compteur pour le texte Unity
app.get('/api/playerCount', (req, res) => {
  res.send(Object.keys(players).length.toString());
});

// phase du jeu (debug)
app.get('/api/gamePhase', (req, res) => {
  res.json({ phase: gamePhase });
});

// Liste des joueurs PARTICIPANTS pour Unity (uniquement les "alive")
app.get('/api/players', (req, res) => {
  if (gamePhase !== 'PLAYING') {
    return res.json({ players: [] });
  }

  const result = [];
  for (const id of currentRoundPlayerIds) {
    const p = players[id];
    if (p && p.state === 'alive') {
      result.push({
        id: p.id,
        h: p.h,
        v: p.v,
        push: p.push
      });
    }
  }

  res.json({ players: result });
});

// Lancement de la manche : snapshot des participants
app.post('/api/startRound', (req, res) => {
  currentRoundPlayerIds = new Set();
  for (const idStr of Object.keys(players)) {
    const id = Number(idStr);
    currentRoundPlayerIds.add(id);
    players[id].state = 'alive';
  }

  const n = currentRoundPlayerIds.size;
  gamePhase = 'PLAYING';
  maxGhostsActive = computeMaxGhosts(n);

  console.log(`Nouvelle manche avec ${n} joueurs. maxGhostsActive=${maxGhostsActive}`);

  io.emit('roundStarted', {
    phase: gamePhase,
    participants: Array.from(currentRoundPlayerIds),
    maxGhostsActive
  });

  res.json({
    ok: true,
    phase: gamePhase,
    participantCount: n,
    maxGhostsActive
  });
});

// Notification de mort envoyée par Unity
app.get('/api/playerEliminated', (req, res) => {
  const playerId = Number(req.query.id);
  const p = players[playerId];

  if (!p || gamePhase !== 'PLAYING' || !currentRoundPlayerIds.has(playerId)) {
    return res.json({ ok: false });
  }

  if (p.state !== 'alive') {
    return res.json({ ok: false });
  }

  console.log(`Unity signale l'élimination du joueur #${playerId}`);

  handlePlayerEliminated(playerId);

  res.json({ ok: true });
});

// Unity lit les pièges à appliquer
app.get('/api/traps', (req, res) => {
  const trapsToSend = pendingTraps;
  pendingTraps = [];
  res.json({ traps: trapsToSend });
});

// --------------------- Loterie Fantôme ---------------------

function handlePlayerEliminated(playerId) {
  const p = players[playerId];
  if (!p) return;

  //le joueur est mort pour la manche
  p.state = 'dead';

  const aliveCount = getAliveCount();
  const ghostCount = getGhostCount();

  const socket = socketsByPlayerId[playerId];
  if (!socket) return;

  // informer la manette qu'elle entre en loterie
  socket.emit('enterLottery');

  // si plus qu'un seul vivant -> pas de fantôme
  if (aliveCount < 2) {
    socket.emit('ghostResult', { selected: false });
    return;
  }

  // si on a atteint le max de fantômes -> pas de fantôme
  if (ghostCount >= maxGhostsActive) {
    socket.emit('ghostResult', { selected: false });
    return;
  }

  // tirage au sort
  if (Math.random() < ghostChance) {
    p.state = 'ghost';
    socket.emit('ghostResult', { selected: true });
    socket.emit('enterGhostMode');
    console.log(`Joueur #${playerId} devient fantôme`);
  } else {
    socket.emit('ghostResult', { selected: false });
  }
}

const PORT = 3000;
server.listen(PORT,"0.0.0.0", () => {
  console.log(`Serveur démarré sur http://localhost:${PORT}`);
});