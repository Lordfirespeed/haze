#!/usr/bin/env node

import { randomBytes } from "node:crypto"
import { promisify } from "node:util"

const randomBytesAsync = promisify(randomBytes)

const foo = new WebSocket("ws://localhost:5000/api/websocket")

async function makeMessageId() {
  var bytes = await randomBytesAsync(6);
  return bytes.toString("hex")
}

async function afterOpen() {
  console.log("heck yeah we open")
  foo.send(JSON.stringify({"$type": "new-session-v1", "id": await makeMessageId()}))
  foo.send(JSON.stringify({"$type": "authenticate-v1", "id": await makeMessageId(), "client_id": "fancy-client", "client_secret": "381y/OIyef4kZnxlWEIno61x+cl1UFk+n2Gttonzu+jJN9GL5nsz9fxJDmOqXwPQ"}))
  foo.send(JSON.stringify({"$type": "whoami-v1", "id": await makeMessageId()}))
}

foo.addEventListener("message", function(event) {
  console.log(`message: ${event.data}`);
})
foo.addEventListener("close", function(event) {
  console.log(`close: ${event.code}, ${event.reason}`)
})
foo.addEventListener("error", function(event) {
  console.log(`an error occurred`)
})

foo.addEventListener("open", (event) => afterOpen());
if (foo.readyState === foo.OPEN) afterOpen();
