# skojjt-3
New skojjt repo with ScoutID login and Python3

## Building an appengine app in Python 3

This code comes from Google and is described at a Google page about

[building-app](https://cloud.google.com/appengine/docs/standard/python3/building-app)

We are working in "building-an-app-4"

We want to tweak it to add ScoutID/SAML log in.

The source code is available via

    $ git clone https://github.com/GoogleCloudPlatform/python-docs-samples

## Setup

    $ python3 -m venv env
    $ source env/bin/activate
    $ pip install -r requirements.txt

## Run locally

Run local a local datastore emulator run

    $ gcloud beta emulators datastore start

Then you need to make the main.py use this datastore.
In a shell, that should be possible by setting an environment variable:

    $ export DATASTORE_EMULATOR_HOST=localhost:8081

After that, one can run (and debug) the app locally with

    $ python main.py

(with python being the virtual env version set above.)

## Deploy to the cloud

The index(es) need to be created:

    $ gcloud datastore indexes create index.yaml

After that, we can deploy with

    $ gcloud app deploy app.yaml --project skojjt-3

Access

https://skojjt-3.ew.r.appspot.com

## Returned data in JSON blob

The call to `google.oauth2.id_token.verify_firebase_token` returns a `claims` dict.
From it, we can extract user data as

    user_data = claims['firebase']['sign_in_attributes']

The data of that user_data dict looks like the following

```python
{'sub': '1000000@www.scoutnet.se', 'firstname': 'Torbjörn',
'role': ['*:*:board_member', '*:*:boat_committee', '*:*:boat_responsible', '*:*:company_signatory', '*:*:grants_agent', '*:*:it_manager', '*:*:leader', '*:*:member_registrar', '*:*:project_admin', '*:*:safety_responsible', 'group:*:*', 'group:*:board_member', 'group:*:boat_committee', 'group:*:boat_responsible', 'group:*:company_signatory', 'group:*:grants_agent', 'group:*:it_manager', 'group:*:member_registrar', 'group:*:safety_responsible', 'group:740:*', 'group:740:board_member', 'group:740:boat_committee', 'group:740:boat_responsible', 'group:740:company_signatory', 'group:740:grants_agent', 'group:740:it_manager', 'group:740:member_registrar', 'group:740:safety_responsible', 'project:*:*', 'project:*:leader', 'project:*:project_admin', 'project:751:*', 'project:751:leader', 'project:751:project_admin'],
'group_name': 'Sjöscoutkåren S:t Göran', 'displayName': 'Torbjörn Einarsson',
'roles': {"organisation":[],"region":[],
  "project":{"751":{"65":"leader","138":"project_admin"}},"network":[],"corps":[],"district":[],
  "group":{"740":{"136":"it_manager","17":"grants_agent","9":"member_registrar","49":"company_signatory","15":"board_member","135":"safety_responsible","337":"boat_committee","35":"boat_responsible"}},
"troop":[],"patrol":[]}',
 'above_15': '1', 'lastname': 'Einarsson', 'group_no': '1135', 'uid': '1000000', 'group_id': '740', 'dob': '2000-01-01', 'email': 'torbjorn.einarsson@stgscout.se', 'firstlast': 'torbjorn.einarsson'}
```

We should let all people with a `leader` or `member_registrar` role have access to skojjt for the corresponding
`group` (kår).

Code like the following should work for that
```python

    accessible_group_ids = []
    for group_id, roles in user_data['roles']["group"]:
        for role_id, role_name in roles:
            if role_name == "leader" or role_name == "member_registrar:
                accessible_groups_ids.append(group_id)
```

We may also want to include assistant leaders.

Similarly, we may want automatically detect administrators from a role in Scoutnet
like `it_manager`.

