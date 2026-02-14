import {createPublicizer} from "publicizer";

export const publicizer = createPublicizer("Terra_Namp");

publicizer.createAssembly("tModLoader").publicizeAll();
publicizer.createAssembly("FNA").publicizeAll();