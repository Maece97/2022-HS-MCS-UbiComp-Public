import {
  buildThing,
  getDatetime,
  getFile,
  getSolidDataset,
  getSourceUrl,
  getStringNoLocale,
  getThing,
  overwriteFile,
  saveSolidDatasetAt,
  setThing,
  ThingPersisted,
  universalAccess,
} from "@inrupt/solid-client";
import { PROV_O, SCHEMA_INRUPT } from "@inrupt/vocab-common-rdf";
import axios from "axios";
import bodyParser from "body-parser";
import * as dotenv from "dotenv";
import express from "express";
import _ from "lodash";
import WebSocket from "ws";
import { getAuthFetch } from "../interactingSolidCommunityServer";
import { Blob } from "buffer";
dotenv.config();

class CurrentActivity {
  readonly activity: string;
  readonly time: Date;
  readonly probability: number;
  constructor(
    activity: string | null,
    time: Date | null,
    probability: number | null
  ) {
    this.activity = activity || "unknown";
    this.time = time || new Date();
    this.probability = probability || 0;
  }

  public static fromRDFObject(thing: ThingPersisted) {
    let prob: any =
      thing?.predicates[
        "http://bimerr.iot.linkeddata.es/def/occupancy-profile#probability"
      ].literals;
    prob = prob["http://www.w3.org/2001/XMLSchema#float"]
      ? prob["http://www.w3.org/2001/XMLSchema#float"][0]
      : 0;

    return new CurrentActivity(
      getStringNoLocale(thing, SCHEMA_INRUPT.name),
      getDatetime(thing, PROV_O.atTime),
      prob
    );
  }

  public updateRDFObject(thing: ThingPersisted) {
    let activity = "";
    switch (this.activity) {
      case "Read action":
        activity = "http://schema.org/ReadAction";
        break;
      case "Search action":
        activity = "http://schema.org/SearchAction";
        break;
      case "Inspection action":
        activity = "http://schema.org/CheckAction";
        break;
    }

    let t = buildThing(thing)
      .setStringNoLocale(SCHEMA_INRUPT.name, this.activity)
      .setDatetime(PROV_O.atTime, this.time)
      .setStringNoLocale(PROV_O.Activity, activity)
      .build();

    t = _.cloneDeep(t);

    //@ts-ignore
    t.predicates[
      "http://bimerr.iot.linkeddata.es/def/occupancy-profile#probability"
    ].literals["http://www.w3.org/2001/XMLSchema#float"] = [
      this.probability.toString(),
    ];
    return t;
  }
}

// const url = "https://solid.interactions.ics.unisg.ch"
const url = "http://localhost:3000";

const subscribeToActivities = [
  "https://solid.interactions.ics.unisg.ch/Marcel/profile/card#me",
];

const getCurrentActivity = async () => {
  const a: any = {};
  const fetch = await getAuthFetch();

  await Promise.all(
    subscribeToActivities.map(async (person) => {
      const u = `${url}/${person.split("/")[3]}/gazeData/currentActivity.ttl`;
      const myDataset = await getSolidDataset(
        u,
        // @ts-ignore
        { fetch: fetch } // fetch from authenticated session
      );

      const activityThing = getThing(myDataset, `${u}`);

      if (!activityThing) {
        return;
      }

      a[person] = CurrentActivity.fromRDFObject(activityThing);
    })
  );

  return a;
};

const tempGazeData: string[] = [];

const saveCurrentActivity = async (currentActivity: CurrentActivity) => {
  const fetch = await getAuthFetch();

  const u = `${url}/Marcel/gazeData/currentActivity.ttl`;
  let myDataset = await getSolidDataset(
    u,
    // @ts-ignore
    { fetch: fetch } // fetch from authenticated session
  );

  const activityThing = getThing(myDataset, `${u}`);

  if (!activityThing) {
    return;
  }

  myDataset = setThing(
    myDataset,
    currentActivity.updateRDFObject(activityThing)
  );

  const savedSolidDataset = await saveSolidDatasetAt(
    u,
    myDataset,
    //@ts-ignore
    { fetch: fetch } // fetch from authenticated Session
  );
};

const saveRawGazeData = async (data: string) => {
  // TODO better save not with every new gaze record but in batches
  const fetch = await getAuthFetch();
  const file = await getFile(
    `${url}/Marcel/gazeData/rawGazeData.csv`, // File in Pod to Read
    { fetch: fetch } // fetch from authenticated session
  );
  let content = await file.text();
  content += data + "\n";

  try {
    const savedFile = await overwriteFile(
      `${url}/Marcel/gazeData/rawGazeData.csv`, // URL for the file.
      // new Blob([content], { type: "text/csv" }), // Buffer containing file data
      Buffer.from(content, "utf8"),
      {
        contentType: "text/csv",
        fetch: fetch,
      } // mimetype if known, fetch from the authenticated session
    );
    console.log(`File saved at ${getSourceUrl(savedFile)}`);
  } catch (error) {
    console.error(error);
  }
};

const updateCurrentActivity = async () => {
  const a = _.cloneDeep(tempGazeData);
  tempGazeData.length = 0;
  const response = await axios.post(`http://localhost:3002`, {
    gazeData: a.join(";"),
  });
  const data = response.data;
  // console.log("response", data);
  saveCurrentActivity(
    new CurrentActivity(data.activity, new Date(data.time), data.probability)
  );
};

const handleIncomingGazeData = (data: string) => {
  tempGazeData.push(data);
  saveRawGazeData(data);
  // console.log("data received ", data.toString());
  if (tempGazeData.length >= 500) {
    updateCurrentActivity();
  }
};

const grantAccessTo = async (webId: string) => {
  console.log("granting access to ", webId);
  const fetch = await getAuthFetch();
  await universalAccess.setAgentAccess(
    `${url}/Marcel//gazeData/currentActivity.ttl`, // Resource
    webId, // Agent
    { read: true }, // Access object
    // @ts-ignore
    { fetch: fetch } // fetch function from authenticated session
  );
};

const connections: WebSocket[] = [];

// Websocket server
const wss = new WebSocket.Server({ port: 3001 }, () => {
  console.log("server started");
});
wss.on("connection", async (ws) => {
  connections.push(ws);
  ws.on("message", (data) => {
    // console.log("received: %s", data);
    handleIncomingGazeData(data.toString());
  });
  console.log("connected");
  ws.send(JSON.stringify(await getCurrentActivity()));
});
wss.on("listening", () => {
  console.log("listening on 3001");
});

// Subscribe to solid update events
const notify = async () => {
  let socket = new WebSocket("ws://localhost:3000/", ["solid-0.1"]);
  // TODO: optionally also subscribe to pods from other users we track.
  socket.onopen = function () {
    this.send(`sub ${url}/Marcel/gazeData/currentActivity.ttl`);
  };
  socket.onmessage = async (msg) => {
    if (msg.data && msg.data.slice(0, 3) === "pub") {
      // console.log("pub", msg.data);
      console.log("currentActivity updated!!!!");
      connections.map(async (ws) =>
        ws.send(JSON.stringify(await getCurrentActivity()))
      );
    }
  };
};

notify();

// Http server
const app = express();
const port = 3003;
app.use(bodyParser.urlencoded({ extended: false }));

app.get("/", (req, res) => {
  res.send("Hello World!");
});

app.post("/grant-access", async (req, res) => {
  await grantAccessTo(req.body.grantAccessTo);
  res.send("Ok");
});

app.listen(port, () => {
  console.log(`Example app listening on port ${port}`);
});
