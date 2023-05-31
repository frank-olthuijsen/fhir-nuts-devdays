# DevDays 2023: Nuts x FHIR

This repo is part of the HL7 FHIR DevDays 2023 tutorial [FHIR x Nuts: How to combine FHIR and Nuts to create large-scale distributed networks for healthcare information exchange](https://www.devdays.com/share/?session=2975161).

The code in this repo is the outcome of the participation in the [Hackathon FHIR x NUTS bij HL7 Working Group meeting](https://www.youtube.com/watch?v=qeadC5w9oR4). As such, this code is by no means production ready. It is solely intended as a PoC. 

However, due to the complexity of setting up and interacting with multiple Nuts nodes and FHIR servers, I believe that this repo can be of value to anyone wanting to learn more about/get started with the **Toepassing op Nuts BgZ**.

The repo consists of:

* 1 Docker compose file that sets up 2 Nuts nodes, 2 Firely Server instances using SQLite and one instance of Seq.
* 1 exported Postman collection and 2 exported Postman environments for settings up the Nuts nodes and FHIR servers.
* 1 Visual Studio solution containing a Firely Server plugin that handles both the sending and receiving of a referral using a notified pull.
* This readme file containing detailed instructions on how to setup all of the above.

This repo was tested on both Ubuntu and Windows 10.

## 1. Installation

### Prerequisites

For this demo we will be making use of [Postman](https://www.postman.com/downloads/), [Docker](https://www.docker.com/) and an IDE that supports C# like [Visual Studio](https://visualstudio.microsoft.com/downloads/) or [Rider](https://www.jetbrains.com/rider/).

### Git repo

The starting point of this tutorial is cloning the [fhir-nuts-devdays](https://github.com/frank-olthuijsen/fhir-nuts-devdays.git) Git repo:

`git clone https://github.com/frank-olthuijsen/fhir-nuts-devdays.git`

### Postman

In the root of the repo you will find a folder named `Postman`. In there are one exported Postman collection and two exported Postman environments. Please import all three of them. The Postman requests are grouped similar to the chapters in this readme.

From now on we will refer the Postman collection as `Postman` and the environments as `Nuts-1` or `Nuts-2`.

### FHIR server

1. Navigate to https://simplifier.net/downloads/firely-server, log in and download an evaluation license for Firely Server using the **Download key**-button. Do not change the name and store the license file in: 

`fhir-nuts-devdays/shared/config/fhir`

2. Navigate to the root of the repo you cloned. It contains a file called `docker-compose.yml`. Run the following command:

`docker-compose up`

The following containers are now running:

* node-one: Nuts node instance
* node-two: Nuts node instance
* fhir-one: Firely Server instance
* fhir-two: Firely Server instance
* seq: Seq instance

3. Inspect the status of each FHIR server:

* [fhir-one](http://localhost:4080)
* [fhir-two](http://localhost:4081)

### Nuts node

1. Inspect the status of each Nuts node:

* [node-one](http://localhost:1323/status/diagnostics)
* [node-two](http://localhost:2323/status/diagnostics)

## 2. Vendor setup

### Setting up the vendor

For this tutorial we will assume two vendors (Vendor-One and Vendor-Two) will each run their own node (node-one and node-two) to service one of their customers (Customer-One and Customer-Two).

Repeat the steps below for each Postman environment (i.e. Vendor): `nuts-1` and `nuts-2`.

### [A. Create a vendor DID](https://nuts-node.readthedocs.io/en/stable/pages/getting-started/3-configure-your-node.html#registering-and-configuring-node-did)

1. Create a new DID:

`Postman: A1. Create a vendor DID`

2. Update the {{VENDORDID}} in the relevant Postman environment.

```json
"id": "did:nuts:5gTvUacW5QVXW4hgFnJYe5gb5uEdo6WUSeYj5p6RABpJ",
```

3. Open `./one/config/node/nuts.yaml` or `./two/config/node/nuts.yaml` depending on the active Postman environment, update the `network`-setting and save the file:

```yaml
network:
  nodedid: did:nuts:8vK61KJLErLVsPoTL8H7sAz8d2w2wDQMoVvnhQmkGf6d
```

4. [Set the vendor contact information](https://nuts-node.readthedocs.io/en/stable/pages/getting-started/4-connecting-crm.html#setting-vendor-contact-information)

`Postman: A4. Add contact information to the vendor DID`

### [B. Create vendor endpoints](https://nuts-node.readthedocs.io/en/stable/pages/getting-started/4-connecting-crm.html#adding-endpoints)

Next we will add a number of services to the vendor DID documents. This will allow other us to reference these services from the customer DID documents that we will add later. This in turn will tell other nodes where to find these services for the relevant customer.

1. Create a NutsComm service which specifies the gRPC address other nodes will use to connect to your node:

`Postman: B1. Add a NutsComm service to the vendor DID`

2. Create a FHIR service:

`Postman: B2. Add a FHIR service to the vendor DID`

3. Create an OAuth service

`B3. Add an OAuth service to the vendor DID`

4. Create a notification service

For the BgZ use case using a notified pull, a notification service (endpoint) is required. For this demo we will use the **fhir-server** service as a notification endpoint. Therefore, we don't need to create a separate service on the vendor DID. A reference to the **fhir-server** service will be added to the organization DIDs.

### Important

**Before continuing with the next steps**:

1. Please make sure you have added **both** vendors using the steps described above
    1. Change the environment in Postman from `Nuts-1` to `Nuts-2`
    2. Go back to the step A1 and work through all of the steps
2. Restart the containers:
    1. `Ctrl+C`
    2. `docker-compose down`
    3. `docker-compose up`

## 3. Organization setup

### Setting up an organization

As mentioned earlier, in this tutorial we will have two vendors with each one organization (or customer). Each organization must be registered with its own DID and DID Document.

Repeat the steps below for each Postman environment (i.e. Vendor): `nuts-1` and `nuts-2`.


### [C. Create a customer DID](https://nuts-node.readthedocs.io/en/stable/pages/getting-started/4-connecting-crm.html#create-and-store-a-customer-did)

1. Create a new DID:

`Postman: C1. Create an organization DID`

2. Update the {{CUSTOMERDID}} variable of the relevant environment.

```json
"id": "did:nuts:31XVYF4urH86D8ok8wzm98Vz4YTsNd7jnJfKnhjd8nTv",
```

### [D. Issue a Nuts Organization Credential](https://nuts-node.readthedocs.io/en/stable/pages/getting-started/4-connecting-crm.html#issue-a-nuts-organization-credential)

After registering a customer, its presence on the network and in the Nuts registry is only a DID. In order for other organizations to find the correct DID and connected services, credentials should be issued and published over the network. For this, the _NutsOrganizationCredential_ can be issued.

1. Create an organization credential

`Postman: D1. Issue an organization credential`

### E. Connect to vendor services

1. Connect to NutsComm

`Postman: E1. Connect to vendor NutsComm`

2. Search for all organization credentials to ensure the registrations have gone well.

`Postman: y. Search for organization credentials`

### [F. Enabling a bolt](https://nuts-node.readthedocs.io/en/stable/pages/getting-started/4-connecting-crm.html#enabling-a-bolt)

Organizations can be found on the network and endpoints have been defined. Now it’s time to enable specific bolts so users can start using data from other organizations. Every bolt requires its own configuration. This configuration is known as a Compound Service on the organization’s DID document. A Compound Service defines certain endpoint types and which endpoint to use for that type.

1. Add a compound service for the *bgz-sender*

`Postman: F1. Enabling a bolt: bgz-sender`

2. Add a compound service for the *bgz-receiver*

`Postman: F2. Enabling a bolt: bgz-receiver`

3. Search for the relevant organization by its name and check that the compound services have been added.

`Postman: z. Search for organization by name`

### [G. Trusting other vendors as issuer](https://nuts-node.readthedocs.io/en/stable/pages/getting-started/4-connecting-crm.html#trusting-other-vendors-as-issuer)

A node operator must not blindly trust all the data is published over the network. Before credentials can be found, the issuer has to be trusted. By default, no issuers are trusted.

1. List the untrusted vendors

`G1. List untrusted vendors`

2. Add the **other** node as a trusted vendor

`Postman: G2. Trust the other vendor`

3. Repeat step G1 to ensure that the vendor is trusted.

### Important

**Before continuing with the next steps**:

1. Please make sure you have added **both** vendors using the steps described above
    1. Change the environment in Postman from `Nuts-1` to `Nuts-2`
    2. Go back to the step C1 and work through all of the steps

## 4. FHIR setup

1. Change the Postman environment to `Nuts-1`. All of the steps below need to be executed for `Nuts-1` only,

1. Load the resources to exchange into `fhir-one`:

`Postman: 1. Load resources`

2. Load the workflow Task to exchange into `fhir-one`:

`Postman > Nuts-1 > 2. FHIR setup > 2. Create workflow Task`

## 5. Flow

1. Send out the referral:

`Postman: 1. Send out referral notification`
