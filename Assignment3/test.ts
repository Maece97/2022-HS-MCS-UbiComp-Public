import * as dotenv from "dotenv";
dotenv.config();
import axios from "axios";

// delete the old tokens
const main = async () => {
  const url = "https://solid.interactions.ics.unisg.ch/idp/credentials/";

  const r = await axios.post(url, {
    email: process.env.EMAIL,
    password: process.env.PW,
  });

  for (const x of r.data) {
    console.log(x);
    const t = await axios.post(url, {
      email: process.env.EMAIL,
      password: process.env.PW,
      delete: x,
    });
    console.log(t.status);
  }
};

main();
