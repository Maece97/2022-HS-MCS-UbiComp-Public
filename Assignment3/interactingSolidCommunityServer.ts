import * as dotenv from "dotenv";
dotenv.config();
import {
  createDpopHeader,
  generateDpopKeyPair,
} from "@inrupt/solid-client-authn-core";
import { buildAuthenticatedFetch } from "@inrupt/solid-client-authn-core";
import * as fs from "fs";
import {
  universalAccess,
  getSolidDatasetWithAcl,
  getAgentAccess,
} from "@inrupt/solid-client";

//import fetch from "node-fetch";
import fetch from "cross-fetch";
import { userInfo } from "os";

// const url = "https://solid.interactions.ics.unisg.ch"
const url = "http://localhost:3000";

let authFetch: (
  input: RequestInfo,
  init?: RequestInit | undefined
) => Promise<Response>;

const getSecret = async (): Promise<any[]> => {
  const response0 = await fetch(`${url}/idp/credentials/`, {
    method: "POST",
    headers: { "content-type": "application/json" },
    // The email/password fields are those of your account.
    // The name field will be used when generating the ID of your token.
    body: JSON.stringify({
      email: process.env.EMAIL,
      password: process.env.PW,
      name: "token1",
    }),
  });

  // console.log(await response0);

  // These are the identifier and secret of your token.
  // Store the secret somewhere safe as there is no way to request it again from the server!
  const { id, secret } = await response0.json();
  // console.log("--This is id", id, "This is secret: ", secret);

  return [id, secret];
};

const getToken = async (id: any, secret: any): Promise<any[]> => {
  // A key pair is needed for encryption.
  // This function from `solid-client-authn` generates such a pair for you.
  const dpopKey = await generateDpopKeyPair();

  // These are the ID and secret generated in the previous step.
  // Both the ID and the secret need to be form-encoded.
  const authString = `${encodeURIComponent(id)}:${encodeURIComponent(secret)}`;
  // This URL can be found by looking at the "token_endpoint" field at
  const tokenUrl = `${url}/.oidc/token`;
  const response = await fetch(tokenUrl, {
    method: "POST",
    headers: {
      // The header needs to be in base64 encoding.
      authorization: `Basic ${Buffer.from(authString).toString("base64")}`,
      "content-type": "application/x-www-form-urlencoded",
      dpop: await createDpopHeader(tokenUrl, "POST", dpopKey),
    },
    body: "grant_type=client_credentials&scope=webid",
  });

  // This is the Access token that will be used to do an authenticated request to the server.
  // The JSON also contains an "expires_in" field in seconds,
  // which you can use to know when you need request a new Access token.
  const a = await response.json();
  // console.log(a);
  const { access_token: accessToken } = a;

  //console.log("--This is access token:", accessToken);
  // console.log("--This is dpop: ", dpopKey);

  return [dpopKey, accessToken];
};

export const getAuthFetch = async () => {
  const idInfo = await getSecret();
  //console.log("*** The secret and id are: ", idInfo);

  //Get token and key
  const token = await getToken(idInfo[0], idInfo[1]);
  // console.log("*** The token is: ", token);

  return await buildAuthenticatedFetch(fetch, token[1], {
    dpopKey: token[0],
  });
};

// Task 3 a)
const createResource = async (
  name: string,
  body?: string,
  headers?: HeadersInit
) => {
  return await authFetch(`${url}/Marcel/${name}`, {
    method: "PUT",
    body: body || "",
    headers,
  });
};

// Task 4 a)
const patchResource = async (
  name: string,
  body: string,
  headers?: HeadersInit
) => {
  return await authFetch(`${url}/Marcel/${name}`, {
    method: "PATCH",
    body: body || "",
    headers,
  });
};

const runAsyncFunctions = async () => {
  //Get id an secret
  const idInfo = await getSecret();
  //console.log("*** The secret and id are: ", idInfo);

  //Get token and key
  const token = await getToken(idInfo[0], idInfo[1]);
  // console.log("*** The token is: ", token);

  authFetch = await buildAuthenticatedFetch(fetch, token[1], {
    dpopKey: token[0],
  });

  // Task 2
  const response = await authFetch(`${url}/Marcel/.acl`);

  console.log(await response.text());

  // Task 3 a)
  await createResource("test/");
  await createResource("gazeData/");
  // Task 3 b)
  await createResource("test/myhobbies.txt", "fun");
  // Task 3 c)
  await createResource(
    "test/.acl",
    `@prefix acl: <http://www.w3.org/ns/auth/acl#>. <#owner> a acl:Authorization;acl:default <./>;acl:accessTo <./>;acl:mode acl:Read, acl:Write, acl:Control;acl:agent <${url}/Marcel/profile/card#me>.`,
    { "Content-Type": "text/n3" }
  );
  await createResource(
    "gazeData/.acl",
    `@prefix acl: <http://www.w3.org/ns/auth/acl#>. <#owner> a acl:Authorization;acl:default <./>;acl:accessTo <./>;acl:mode acl:Read, acl:Write, acl:Control;acl:agent <${url}/Marcel/profile/card#me>.`,
    { "Content-Type": "text/n3" }
  );
  await createResource(
    "test/myhobbies.txt.acl",
    `@prefix acl: <http://www.w3.org/ns/auth/acl#>. <#owner> a acl:Authorization;acl:default <./myhobbies.txt>;acl:accessTo <./myhobbies.txt>;acl:mode acl:Read, acl:Write, acl:Control;acl:agent <${url}/Marcel/profile/card#me>.`,
    { "Content-Type": "text/n3" }
  );
  await createResource(
    "gazeData/.acl",
    `@prefix acl: <http://www.w3.org/ns/auth/acl#>. <#owner> a acl:Authorization;acl:default <./>;acl:accessTo <./>;acl:mode acl:Read, acl:Write, acl:Control;acl:agent <${url}/Marcel/profile/card#me>.`,
    { "Content-Type": "text/n3" }
  );

  // Task 4 a)
  // Before using solid-client.
  // await patchResource(
  //   "test/.acl",
  //   "@prefix solid: <http://www.w3.org/ns/solid/terms#>. @prefix foaf: <http://xmlns.com/foaf/0.1/>. @prefix acl: <http://www.w3.org/ns/auth/acl#>. _:rename a solid:InsertDeletePatch; solid:inserts { <#public> a acl:Authorization;acl:accessTo <./myhobbies.txt>;acl:mode acl:Read; acl:agentClass foaf:Agent. }.",
  //   { "Content-Type": "text/n3" }
  // );
  await universalAccess.setAgentAccess(
    `${url}/Marcel/test/myhobbies.txt`, // Resource
    `${url}/kayPod/profile/card#me`,
    { read: true, write: false }, // Access object
    // @ts-ignore
    { fetch: authFetch } // fetch function from authenticated session
  );

  await universalAccess.getAgentAccess(
    `${url}/Marcel/test/myhobbies.txt`,
    `${url}/kayPod/profile/card#me`,
    // @ts-ignore
    { fetch: authFetch } // fetch function from authenticated session
  );

  // Task 4 b)
  await createResource("test/myFriendsInfo.txt", "404 :)");
  await createResource("myFamilyInfo.txt", "404 ;)");

  // Task 5
  await createResource("gazeData/rawGazeData.csv", "");

  await createResource(
    "gazeData/rawGazeData.csv.acl",
    `@prefix acl: <http://www.w3.org/ns/auth/acl#>. <#owner> a acl:Authorization;acl:default <./rawGazeData.csv>;acl:accessTo <./rawGazeData.csv>;acl:mode acl:Read, acl:Write, acl:Control;acl:agent <${url}/Marcel/profile/card#me>.`,
    { "Content-Type": "text/n3" }
  );

  await createResource(
    "gazeData/currentActivity.ttl",
    `@prefix xsd:  <http://www.w3.org/2001/XMLSchema#> .@prefix foaf: <http://xmlns.com/foaf/0.1/> . @prefix prov: <http://www.w3.org/ns/prov#> . @prefix schema: <http://schema.org/> . @prefix bm: <http://bimerr.iot.linkeddata.es/def/occupancy-profile#> . <${url}/Marcel/gazeData/currentActivity.ttl> a prov:Activity, schema:ReadAction; schema:name "Read action"^^xsd:string; prov:wasAssociatedWith <${url}/Marcel/profile/card#me>; prov:used <${url}/Marcel/gazeData/rawGazeData.csv>; prov:endedAtTime "2022-10-14T02:02:02Z"^^xsd:dateTime; bm:probability  "0.87"^^xsd:float. <${url}/Marcel/profile/card#me> a foaf:Person, prov:Agent; foaf:name "Marcel Mettler"; foaf:mbox <mailto:marcelthomas.mettler@student.unisg.ch>.`,
    { "Content-Type": "text/n3" }
  );

  await createResource(
    "gazeData/currentActivity.ttl.acl",
    `@prefix acl: <http://www.w3.org/ns/auth/acl#>. <#owner> a acl:Authorization;acl:default <./currentActivity.ttl>;acl:accessTo <./currentActivity.ttl>;acl:mode acl:Read, acl:Write, acl:Control;acl:agent <${url}/Marcel/profile/card#me>.`,
    { "Content-Type": "text/n3" }
  );
};

runAsyncFunctions();
