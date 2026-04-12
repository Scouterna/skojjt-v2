## **Membership system API** 

The membership system has multiple external interfaces providing data retrieval and interaction through an authentication layer. In order to use the API, system entities (e.g. Groups) must generate an api key. Doing so activates the URL and provides the necessary credentials for gaining access to it. 

Available endpoints are defined in the system configuration (app.yml) file by a system administrator. 

## **Activating an endpoint** 

From the homepage of the entity in question (e.g. group), under the "messaging" tab and "api" submenu item. Here it is possible to enable/disable access to the api interface in general, and also generate/regenerate keys for all the endpoints. 

## **API Keys and URLs** 

Developers of external systems can use a unique url to retrieve live data from the system, or (for example, in the case of the registration endpoint) post data to the system. 

An endpoint url is in this format: https://entityId:apiKey@endpointUrl (Alternatives below) where each part of the url is explained in detail below. 

## **Entity Id** 

The entity id is an internal, permanent id for the body (e.g. group), and will never change. This internal id may or may not correspond to a visible identifier in the system (such as the group number), this should not be assumed. 

For example, to find a group ID, check the group home page URL (or inspect the endpoint example URLs on the API page) 

## **Endpoint Url** 

The endpoint url is a permalink (permanent url that will not change) to one specific endpoint / resource. 

## **API Key** 

The apiKey (which is the only varying part of an endpoint URL), is a unique password for a single endpoint + body. Each endpoint has its own api key, and only provides access to that single endpoint resource. The api key cannot be used in other areas of the systems, such as regular authentication, other endpoints or the same endpoint for a different body. 

The API key can be changed at any time by clicking the "Regenerate key" button on the API overview page for the body. This should be done if the key is compromised in any way. 

## **Communicating with an endpoint** 

All communication with an endpoint must be done securely via SSL (https), and every call is authenticated with the username (body or project/activity id) and password (api key) against the endpoint url. 

When successfully authenticated, you can retrieve information from (or post data to) that endpoint by following the endpoint documentation. Authenticating against an endpoint does not require a user account in the system, as the authentication does not log you in as a user, it simply grants you access to that single resource. 

There are a few methods which can be used to authenticate with an endpoint, and each will be useful in different scenarios. In all cases, as mentioned above, an ID (which body/entity in the system are you trying to get information about) and a key are required. 

## **Basic Auth** 

Authenticate by sending an Authorization header, using the ID and Key as username and password. This can also be done directly in the URL (useful for testing) by passing https://ID:KEY@URL 

## **Bearer token** 

Place the API key in the Authorization header, for example "Bearer sddsvnsdvklnsdv". Pass the ID (if required) as a request parameter (e.g. https://site.com/api/endpoint?id=123) 

Selecting the required resource in this manor is slightly more "REST" oriented, and if it becomes possible to request other resources with the same key in the future, then this method is slightly more future proof, as only the ID parameter would need to be changed. 

## **Request parameters** 

It is also possible to send the ID and Key in the URL, without using an Authorization header - this may be desirable in some cases where manipulating headers, or using basic auth is not available (e.g. interacting with third-party services where only a URL can be entered). (e.g. https://site.com/api/endpoint?id=123&key=sdhbcdjkcbascjb ) 

## **Format of returned data** 

All API data is currently returned in JSON[1] format by default, which can be read and parsed by almost all programming languages and systems. Json is best suited for "live" requests, as the payload is as small as possible, and there is minimal server-side processing. 

Some endpoints offer alternative formats, such as csv, xls and pdf - for convenience. These generally use more system resources, so should be used sparingly (e.g. as part of a batch copy process) - "live" usage should be avoided where possible. 

## **Example JSON response** 

```
{"Group":{"name":"Awesome
speidergruppe","membercount":40},"Leader":{"name":"Lord Robert Stephenson
Smythe Baden-Powel"}
```

## **Example PHP** 

```
$data        = json_decode($jsonString, true);
$groupName   = $data["Group"]["name"];
$memberCount = $data["Group"]["membercount"];
$leaderName  = $data["Leader"]["name"];
```

## **Available endpoints** 

## **View group information (viewGroup)** 

This endpoint provides information and statistics about a group, and also the api key to get project information about the group (if one exists). 

## **Example output** 

```
{
  "Group":{
```

```
    "name":"Awesome group",
    "membercount":40,
    "rolecount":4,
    "waitingcount":3,
    "email":"contact@group.com",
    "description":"Our group is awesome",
    "stats": {
      "active": {
          "sum": 40,
          "breakdown":
          {
            "6": {
                "1": 5
            },
            "7": {
                "1": 10
            },
            "8": {
                "1": 10,
                "2": 15
            }
          }
        },
      "active_paid": {
        "sum": 30,
        "term_id": 8,
        "term_label": "2016",
        "breakdown": {
          "6": {
            "1": 3
          },
          "7": {
            "1": 7
          },
          "8": {
            "1": 8,
            "2": 12
          }
        }
      },
      "active_paid_previous": {
        "sum": 25,
        "term_id": 7,
        "term_label": "2015",
        "breakdown": {
          "6": {
            "1": 3
          },
          "7": {
            "1": 7
          },
          "8": {
            "1": 8,
            "2": 7
          }
        }
      },
        "below_26": 40,
        "generated": "2016-05-19 00:13:37",
        "active_troops": 3
    }
  },
  "Leader":{"name":"Lord Robert Stephenson Smythe Baden-Powel", "contactdetails":"555 55 555"},
  "projects":"http:\/\/1234:123456789@system_name\/api\/organisation\/project"
}
```

## **Explanation of keys** 

- **name** - The name of the group 

- **membercount** - The number of currently active members 

- **rolecount** - The number of active members with roles in this group 

- **waitingcount** - The number of members (if any) on the group waiting list 

- **email** - The value of the "email" contact field (or false if not set) 

- **description** - The group description 

- **stats** 

   - Number of members broken down by current and previous term, age, and gender. 

   - "active" contains all currently active members 

   - The breakdown is by age, then gender selection (1 = male, 2 = female, 3 = other) 

   - "active_paid" contains all currently active members, who have paid in the current term 

   - "active_previous" contains all currently active members, who paid in the previous term 

   - "below_26" is a simple count of members up to and including 26 years old 

   - "generated" is the timestamp when the statistics were last refreshed (normally once per night) 

   - "active_troops" is the number of troops/units currently registered in this group 

- **leader** - contains details of the current group leader (name + phone number) 

- **projects** - contains the URL for the "project info" API endpoint for this group (see below) 

## **View info about projects the group is registered to (viewGroupProjects)** 

**Note: Only projects that have group members from this group registered to them will be returned.** 

## **Example output** 

```
[{"Project":{"name":"Awesome project","starts":"2009-07-04 00:00:00",
"ends":"2009-07-11 00:00:00",
```

```
"description":"It really is an awesome project","updated":"2011-01-20 17:42:54",
"min_age":15,"max_age":0}},{“Project”:{...another project info here...}}]
```

## **Register a group member on a waiting list (RegisterGroupMember)** 

This endpoint provides a method for pushing membership data to the system and registering a new member on the waiting list for a group. 

It is possible to use post data (for example remote form submission) or "get" data (formatted URL string) to pass the data to the system, providing the correct api credentials are present in the URL. 

This process uses the same backend form as the one used in standard registration, so all validation rules, required fields and available input fields from that form apply to this endpoint. 

Remember that if you need to pass information with spaces or newlines (such as an address or notes) it must be properly url-encoded (spaces becomes %20, etc). You should use whatever functionality is available in the programming language used to interact with the api to perform this (for php, you can use urlencode()). 

Any errors triggered by the form are returned in an array with a header status code 400. If the form is processed successfully, an array of the successfully entered values is returned with status code 200, unless a "redirect" parameter was sent with the request, in which case the internal route name value provided by the "redirect" key will be processed instead – this could be for example "homepage". Refer to routing.yml for a list of available route names if required. 

On successful registration, an activation email is sent to the user, as per normal registration. 

Since the registration form and validation requirements are subject to change, they are not listed here – please refer to the system for this information. 

An example of the syntax is shown below (Example registration with correct data). 

## **Example registration with missing data** 

```
https://1234:123456789@system_name/api/organisation/register/member
[400] Response:
 {
  "profile": [
    {
      "key": "ssno",
      "value": null,
      "msg": "Please enter your full social security number"
    },
    {
      "key": "sex",
      "value": null,
      "msg": "You must choose a gender"
    },
    {
      "key": "date_of_birth",
      "value": null,
      "msg": "Födelsedatum Obligatoriskt"
    },
    {
      "key": "first_name",
      "value": null,
      "msg": "First name required"
    },
    {
      "key": "last_name",
      "value": null,
      "msg": "Surname required"
    }
  ],
  "address_list": {
    "address_1": {
        "address_type": {
            "key": "address_1",
            "value": null,
            "subkey": "address_type",
            "msg": "Please choose an address type address_line1"
        }
      }
    }
  }
```

## **Example registration with correct data** 

```
 http://1234:123456789@system_name/api/organisation/register/member?
 profile[first_name]=bob
 &profile[sex]=1
 &profile[email]=russ@custard.no
 &profile[last_name]=something
```

```
 &membership[status]=1
 &address_list[addresses][address_1][address_type]=0
 &address_list[addresses][address_1][address_line1]=Somestreet
 &address_list[addresses][address_1][country_code]=752
 &address_list[addresses][address_1][zip_code]=12345
 &address_list[addresses][address_1][zip_name]=Stockholm
 &contact_list[contacts][contact_1][contact_type_id]=9
 &contact_list[contacts][contact_1][details]=alternative@email.nope
 &contact_list[contacts][contact_2][contact_type_id]=14
 &contact_list[contacts][contact_2][details]=Mommy
 &profile[date_of_birth]=1979-03-10
 &profile[ssno]=0258
 &profile[note]=Some%20interesting%20detail%20here
 &custom_field[123123]=Answer%20to%20your%20question
 &custom_field[324]=1
 &custom_field[32463]=thisisabooleanfield
[200] Response:
{"profile":
  {"sex":null,
   "date_of_birth":"1979-03-10",
   "first_name":"bob",
   "last_name":"something",
   "nick_name":null,
   "newsletter":null,
   "magazine":null,
   "ssno":"0258",
   "force_police_check":null,
   "id":null
  },
 "membership":
  {"troop_id":null,
   "status":2,
   "feegroup_id":null,"patrol_id":null
  },
 "contact_list":{"contacts":
                 {"contact_1":{"contact_type_id":"9",
                               "details":"alternative@email"
                              },
                  "contact_2":{"contact_type_id":"8",
                               "details":"htto://my.blog"
                              }
                 }
  },
 "address_list":{"addresses":
                 {"address_1":{"address_type":"0",
                               "address_line1":"Somestreet",
                               "country_code":"752",
                               "zip_code":"12345",
                               "zip_name":"Stockholm"}
                 }
  },
  "custom_field": {"123123": "Answer to your question","324": "1","32463":"1"}
}
```

## **Example registration with correct data using content-Type:application/json** 

```
{
    "profile":{
        "first_name":"bob",
        "sex":"1",
        "email":"russ@custard.no",
        "last_name":"something",
        "date_of_birth":"1979-03-10",
        "ssno":"0258",
        "note":"Some interesting detail here"
    },
    "membership":{"status":"1"},
    "address_list":{
        "addresses":{
            "address_1":{
              "address_type":"0",
              "address_line1":"Somestreet",
              "country_code":"752",
              "zip_code":"12345",
              "zip_name":"Stockholm"
            }
        }
    },
    "contact_list":{
        "contacts":{
            "contact_1":{"contact_type_id":"9","details":"alternative@email.com"},
            "contact_2":{"contact_type_id":"38","details":"47467087"}
        }
    },
    "custom_data":{"1":"Answer to your question","2":"1","4":"1","5":"0"}
}
```

## **Registration values** 

Sex: 0 = unknown, 1 = male, 2 = female, 3 = other 

## **Which contact type ID?** 

Contact fields are set by the system administrators of each system, so cannot be listed here with any accuracy. 

Until there is a better method for listing them, the easiest approach would be to inspect the html of the contact field dropdown list on the main system. 

A future endpoint will (should) be provided to return a list of all possible contact types, along with their keys and translations. This will also be useful for some of the reporting endpoints, below. 

## **Example registration using a parent body for authentication** 

```
https://1234:123456789@system_name/api/organisation/register/member?auth_body_type=organisation&auth_body_id=692
```

In this case we still prefer to "hide" the API key in the basic auth part, or in a header (Bearer) parameter. However now we are asking the system to check the API key against a specified parent body of the group, in the example above it's the main organisation. The "id" parameter should still belong to the group, and as long as the group "belongs to" the specified parent, it will be returned as normal. 

Currently supported "parents" are organisation and district. Support for corps, region, network can be added quickly if necessary. This will work for any group-based endpoint, providing the parent has the specified key record in the database. 

## **Get a detailed csv/xls/json list of all members** 

This endpoint uses the same backend logic as the standard member reports, providing a way to retrieve a full list of members. 

All members currently on the group waiting list can also be retrieved using this method, by adding the parameter "waiting=1" to the request All members currently on the group awaiting approval list can be retrieved by adding the parameter "awaiting_approval=1" to the request 

If you combine both parameters (waiting=1&awaiting_approval=1) then you will get both sets of members. Any on the waiting list will have a "waiting_since" value, whereas this will be empty for those awaiting approval. There will also be an extra parameter "awaiting_approval" in the response, which will be 0/No or 1/yes. 

## **Json response** 

This is the default response. Adding "pretty=true" as a parameter to the URL will format the json to make it more human-readable. It makes the payload slightly larger, so it's best to do this only when debugging/experimenting. 

- All the member info is contained within a "data" object. 

- Translated labels for the "column" headers are provided in the "labels" object 

- All used contact fields are included (at least one person in the group using the field) 

- "raw_value" is provided for safer consumption by external systems where necessary. For example, to check/store gender, use 1/2/3 not "Male, Female, Other" as these are liable to change with translations 

## **XLS/CSV/pdf response** 

- Same as reports available via the system 

- Use simple=true to get a simple report with limited columns 

- Use format=csv or format=xls to override the default json formatting 

- Use format=pdf to get a pdf (combine with simple=true for best results - predefined column choices) 

- Pdf format without "simple" flag will have limited, undefined columns 

## **Update group membership (E.g. status, troop, patrol) - (UpdateGroupMembership)** 

This endpoint can be used to update a membership in a group - a common scenario would be moving a member from the waiting list into a troop/patrol and confirming their membership status. 

Refer to above section for registering group members on waiting lists, for examples of how to use POST data and how to authenticate via the organisation/district API keys. 

## **Example registration with correct data using content-Type:application/json** 

```
{
    "123456":{"status":"confirmed","troop_id":56},
    "234567":{"status":"waiting"},
    "345678":{"status":"confirmed","patrol_id"12},
    "456789":{"status":"cancelled"},
    "567890":{"status":"confirmed","troop_id":34,"patrol_id":567}
}
```

## **Successful response** 

```
{
  "success":true,
  "updated": [
    123456,
    234567,
    345678,
    456789,
    567890
  ]
}
```

## **Potential error responses** 

Any error will cause the entire request to be rejected. A list of failing member numbers will be provided with one of the following error messages: 

- Invalid troop_id/patrol_id combination (When providing a patrol id and troop id but they do not belong to each other) 

- Invalid patrol_id selection (When patrol_id is not found in the selected group) 

- Invalid troop_id selection (When troop_id is not found in the selected group) 

- Invalid status selection (Status should be confirmed, cancelled, ot waiting) 

- Invalid parameter (Parameter should be status, troop_id, or patrol_id) 

- Membership number passed with no parameters (Member number passed with an empty array, or value is not an array) 

- Membership record not found in group (Member may or may not exist, but the group/member combination does not) 

E.g. 

## **Get a csv/xls/json list of members, based on mailing lists you have set up** 

This powerful endpoint allows you to get xls/csv/pdf reports (as described above) using the rules and filters defined by your mailing lists. 

- Accessing the endpoint with no list_id parameter will return an object containing available lists, along with the URLs for accessing each of them (and their rules) via the API. 

- Use list_id=X to return a list of "subscribers" to list X 

- Use list_id=X&rule_id=Y to return a list of "subscribers" that match rule Y 

- pretty=true is available for json format debugging/viewing 

- use format=csv or format=xls to override the default json format and download report files (use sparingly) 

- pdf output of these lists is not currently available 

- contact fields are limited by default (same as the ones used by simple=true, above) for performance reasons (Mailing lists can in theory contain every member in the system) 

- To override and retrieve a different set of contact data, add contact_fields=key1,key2,key3... to the parameters - Keys are set by the site administrator and vary between systems - The easiest way to find the keys used by members of the interested group, is to use the group list API endpoint and inspect the "labels" object. (Discard "contact_") - e.g. contact_fields=work_phone,instant_messaging,email_dad,email_mum 

## **Get a list of groups with members attending a project** 

Use the project ID and API key to retrieve a list of groups with participating members. The output of this (by default) is identical to the output used by the map data API. 

Response data is nested by organisation --> region --> district --> group 

To retrieve a flat list (indexed by group, with other info such as district as properties) - use the parameter flat=true 

## **Note: The "questions" object is populated with question IDs and responses for all questions which have been selected in the admin group questions interface as "include in API"** 

Available filters: 

- group_id 

- group_ids[] (ignored if group_id passed) 

## **Get a list of patrols with answers to questions** 

Use the project ID and API key to retrieve a list of patrols with participating members. 

Response data is nested by organisation --> region --> district --> group 

To retrieve a flat list (indexed by group, with other info such as district as properties) - use the parameter flat=true 

To restrict the patrols in the list, use patrol_id=12345 or patrol_ids[]=12345&patrol_ids[]=12346 You can also search by case-insensitive patrol name: patrol_name=foo or patrol_names[]=foo&patrol_names[]=bar 

## **Get a list of members who are registered on the project** 

Use the project ID and API key to retrieve a list of members attending the project, along with some key information regarding each member. 

## **Example response** 

```
{
  "participants": {
    "1234567": {
      "member_no": 1234567,
      "group_registration": false,
      "first_name": "Firsty",
      "last_name": "McLasty",
      "checked_in": true,
      "registration_date": "2012-03-06 11:06:22",
      "member_status": 2,
      "cancelled": false,
      "cancelled_date": null,
      "sex": "1",
      "date_of_birth": "1985-01-01",
      "primary_email": "email@myemail.com",
      "group_id": null,
      "group_name": null,
      "org_id": null,
      "org_name": null,
      "district_id": null,
      "district_name": null,
      "patrol_name": null,
      "questions": {100: "Yes please", 101: "64"}
```

```
    }
  },
  "labels": {
    "sex": {
        "1": "Man",
        "2": "Kvinna",
        "0": "Annat"
    },
    "member_status": {
        "1": "Oregistrerad",
        "2": "Aktiv",
        "4": "Ny- inte klar f\u00f6r fakturering",
        "8": "Avliden",
        "16": "Automatiskt avregistrerad",
        "32": "Active non-member"
    }
  }
}
```

## **Note: The "questions" object is populated with question IDs and responses for all questions which have been selected in the admin form-edit interface as "include in API"** 

Filters available: 

- member_number=12345 

- group_id=12345 

- patrol_id=12345 

## **Update the check-in state of participant(s) on the project** 

- Use a PUT request (although POST will also work) 

- Set the content header "Content-Type: application/json" 

- Send only json in the body of the request 

- Comment is optional - status is not 

- Use member numbers as object keys, checked_in 1 or 0 (checked-in vs checked-out respectively) 

- When checking out - use "attended: 0" to mark the member as a non-participant (affects reports/CVs/etc.) 

- When checking in - "attended" will automatically be set to "1" and cannot be overridden 

## **Example request** 

```
{"1234567":
  {"checked_in":0,"comment":"A comment explaining why I am checking out this member"},
 "987654":
  {"checked_in":1},
 "654321":
  {"checked_in":0, "comment":"Oops, this member was never here","attended":0}
}
```

## **Example response** 

```
{
  "checked_in": [
```

```
    987654
  ],
  "checked_out_not_attended": [
    654321
  ],
  "checked_out_attended": [],
  "unchanged": [],
  "not_found": [],
  "no_member": [
    1234567
  ],
  "total":3
}
```

In this case, we tried to remove the check-in state for member 1234567 with a comment, and at the same time check-in member 987654. Member 1234567 was not found in the system at all (no member), member 987654 was checked in successfully. Member 654321 was checked out and at the same time marked as "not attended". This means the member will not show up in project reports once they are inactive in the system, they will not be eligible for CV entries, and they will show up as "attended = no" in reports (whilst they are active). 

Members who are checked out, but retain the "attended" flag will still be treated as "participants". An example usage is leaving the activity early, which is useful to know for planning activities/food - but not so early that the participant should lose their attendance status. 

- Checked_in: Member was checked in 

- Checked_out: Member was checked out 

- Unchanged: Member's state was not changed (already matched the requested state) - comments not saved, no changes made 

- Not_found: Member exists but was not found in this project/activity 

- No_member: Member does not exist in the system 

## **Update question responses for participant(s) on the project** 

This endpoint is shared by the check-in endpoint (above) so can be combined as part of the check-in process, or alternatively it's also possible to send the body of the request with no change to check-in state. 

It's important to supply a "value" for each question, as some question types support a value and subvalue. Subvalue is optional, so in most cases it's enough to just supply a value as shown in the example below. 

Questions must exist on the project, and must be marked as accessible by the API. Boolean questions (e.g. checkboxes) should be supplied with "1" or "0" for checked/unchecked respectively. Questions involving choices (e.g. dropdown, leader select, radios) must supply the value of the choice. See the next endpoint for details on how to get the required values for use by your app/service. 

The response will contain a further node "updated_questions" with details of updates. If a question shows "null" it means that there were no errors, but the question was not updated (most likely because the answer was the same as the existing one). If an update occurs, the array will contain details of previous values and current, new values. 

Any error will cause the entire request to fail with no database changes committed - the response will be a 400 (or 500 in the case of a serious error) with an error explaining the problem. 

Validation of the fields is very light - there is no check for hidden or required fields, and it's possible to supply garbage in some cases at present (e.g. text in a number answer). Not all fields are currently supported (e.g. Subprojects, equipment orders, etc.). 

## **Example request** 

```
{"3000616": {
   "checked_in":"1",
   "questions": {"499":{"value":"301"},"815":{"value":"15:00"}}
  }
}
```

## **Example response** 

```
{
  "checked_in":[
    3000616
  ],
  "checked_out_attended":[],
  "checked_out_not_attended":[],
  "unchanged":[],
  "not_found":[],
  "no_member":[],
  "total":1,
  "updated_questions": {
    "3000616":{
      "499": {
        "id":184815,
        "action":"updated",
        "original_values": {
          "value":"301","subvalue":null
        },
        "new_values":{
          "value":"302","subvalue":null
        }
      },
      "815":{
        "id":184816,
        "action":"updated",
        "original_values":{
          "value":"12:00","subvalue":null
        },
        "new_values":{
          "value":"13:00",
          "subvalue":null
        }
      }
    }
  }
}
```

## **Example Errors (HTTP status code 400):** 

```
{
  "3000616":{
    "checked_in":1,
    "questions": {"123":{"value":"302"},"815":{"value":"13:00"}}
  }
}
```

```
{"error":"Server error! Message: Question with ID 123 is not a valid API
          accessible question for this project (Processing member: 3000616)"}
```

```
{
  "3000616":{
    "checked_in":1,
    "questions": {"499":{"value":"123"},"815":{"value":"13:00"}}
  }
}
{"message":"Saving answers failed - all transactions aborted",
 "errors":{
  "3000616":{
    "499":"Invalid choice (123) -
           please check available options for this question"}
  }
}
```

## **Get all group data for groups in the system (GPS coords, etc)** 

• https://1234:123456789@system_name/api/organisation/group/all 

• **district_ids** - Restrict to certain districts, e.g. https://1234:123456789@system_name/api/organisation/group/all?district_ids[]=693&district_ids[]=694 

• **updated_since** - Only include records updated since the timestamp, e.g. https://1234:123456789@system_name/api/organisation/group/all?updated_since=1601837852 

If the system uses regions, the output will be [region_id][districts][district_id][groups][group_id]{group data} If the system does not use regions, the output will be ["data"][districts][district_id][groups][group_id]{group data} 

Names of municipalities and counties are provided within each district, to prevent duplication. E.g. It would have been better to provide these at the top level, but the legacy "region" level prevents this, without mess. 

[region_id][districts][district_id][municipalities]{municipality data} [region_id][districts][district_id][counties]{municipality data} 

**Note:** Deleted and/or otherwise "unpublished" records will be removed from the output. In a planned update, filtering on "updated_at" will also return null entries for these 

## **Get all published role members** 

- https://1234:123456789@system_name/api/organisation/published_roles/all 

• **updated_since** - Only include records updated since the timestamp, e.g. https://1234:123456789@system_name/api/organisation/published_roles/all?updated_since=1601837852 Output is split into role_data, member_data, and role_names, to avoid duplication. 

role_data contains all the published roles grouped by organisation level (e.g. group, organisation, district), in each organisation level the members are listed. Data about the individual members (name, contact info, profile pic, etc.) can then be cross referenced using the member ID in the member_data array. Role IDs are also provided in the role list so their names can be retrieved from the "role_names" array. 

"Updated" records includes any changes to the role entry (e.g. the about text) along with changes to the member profile, for example a name change or "about yourself" change. Some changes may not be relevant, for example if a member changes their date of birth it will be returned as an "updated" record, even though there will be no visible changes in this particular API output. A later update could use historical data to see what changed, and only include results with changes to relevant fields, but that is not planned. 

**Note:** Deleted and/or otherwise "unpublished" records will be removed from the output. In a planned update, filtering on "updated_at" will also return null entries for these 

## **Get published projects/events/courses** 

- https://1234:123456789@system_name/api/project/get/published 

- **updated_since** - Only include records updated since the timestamp, e.g. https://1234:123456789@system_name/api/project/get/published?updated_since=1601837852 

**Note:** Deleted and/or otherwise "unpublished" records will be removed from the output. In a planned update, filtering on "updated_at" will also return null entries for these 

## **Get triggered emails** 

- https://1234:123456789@system_name/api/admin/triggered_emails?template_key=xxx 

- **template_key** - Mandatory parameter identifying the email template defining the triggers 

- **since** - Timestamp to filter results created since. If not provided, results are restricted to start of current day 

## **Example Output:** 

```
{
  "data": {
    "18287": {
      "recipient_member_number": 123456,
      "recipient_first_name": "Ivar",
      "recipient_last_name": "Brumpton",
      "recipient_emails": [
        "api@custard.no"
      ],
      "status": 16,
      "trigger_date": "2022-10-12 18:24:04",
      "trigger_details": {
        "1": {
          "group": {
            "4027": {
              "roles": {
                "1": {
                  "id": 1,
                  "timestamp": 1663095225,
                  "sr_id": 56038
                }
              }
            }
          }
        }
      }
    },
    "DATABASE ID": {
      "recipient_member_number": MEMBER NUMBER OF TRIGGERED MEMBER,
      "recipient_first_name": "FIRST NAME OF MEMBER",
      "recipient_last_name": "LAST NAME OF MEMBER",
      "recipient_emails": [
        LIST OF EMAILS (Primary/contact based on settings)
```

```
      ],
      "status": SEE STATUS CODES BELOW,
      "trigger_date": "DATE OF TRIGGER - E.G. WHEN THE TASK WAS RUN",
      "trigger_details": {
        "TRIGGER_SEQUENCE": {
          "RELEVANT BODY TYPE": {
            "RELEVANT BODY ID": {
              "roles": {
                "ROLE ID": {
                  "id": ROLE ID,
                  "timestamp": TIME WHEN ROLE WAS GRANTED,
                  "sr_id": DATABASE ID OF ROLE ASSIGNMENT RECORD
                }
              }
            }
          }
        }
      }
    }
  }
}
```

## **Statuses:** 

- **1** - Email sent 

- **2** - Record created directly (no email sent) - e.g. during a pre-population task to fill in old records and prevent triggering 

- **4** - Sending email failed 

- **8** - Entering the record directly failed (Record exists but has some errors/inconsistencies) 

- **16** - No errors but email was not sent (e.g. When trigger is set to "collate only" in settings) 

http://www.json.org/ 

1 


## **Membership system Authentication API** 

This API is primarily used for providing access for an individual, rather than a machine. For example an external system can ask a member to "log in" to the membership system to verify who they are, what memberships they have, their roles, etc. 

This API could also be used by app developers to provide a way for a member to log in and access data they would normally have access to in the membership system. This is preferable to using API keys which tend to grant access to entire body (group, etc.), they are best suited to machine <--> machine communication. 

## **Endpoints summary** 

- /api/authenticate - Send a username and password to get a token in return. This is the only endpoint that does not require an "Authorization" header 

- /api/get/profile - Get basic profile info about the authenticated member 

- /api/refresh_token - Request a new token (See Token expiry, below) 

- /api/get/user_access_list - Get a list of system-defined access keys, based on permissions (coded, customer specific) 

- /api/get/user_roles - Get a list of roles the authenticated member has 

- /api/get/profile_image - Get the authenticated (by token) member's profile image 

- /api/check_permission - Check whether the currently authenticated user can perform a certain action on a system body 

- /api/get/memberships - A detailed list of memberships, with details of membership bodies 

- /api/get/memberlist/:bodyType/:body_id - A detailed list of members for a specified body (e.g. group) 

- /api/get/projects/available - A listing of projects/activities/courses the member can register for 

- /api/get/projects/registered - A listing of projects/activities/courses the member has already signed up for (includes past records) 

## **Tokens** 

## **Using the token** 

Following a request to /api/authenticate with a valid username and password, the system returns a JWT as a JSON body. This contains a basic payload along with a token required for further requests. 

It is also possible to post the following (optional) fields when submitting the username/password: 

- app_id - If 10 characters or more, identifies the app to the membership system, and allows for persistent keys (e.g. a UUID or some other string that is likely to be unique in the wild) 

- app_name - Members will be able to view and revoke keys via the membership system. Supplying an app name to accompany the ID will make it easier to identify which key is associated with each service 

- device_name - Send this along with the form to also aid with the above token management (e.g. "My mobile" or "Chrome on my PC"), since the member will need to authenticate on every unique "user agent" 

Omitting the above fields will simply show an "unknown" login on an "unknown" device. Omitting the app_id will result in an expiring key (default 10 minutes). 

Use the token by adding an "Authorization" header with the word "Bearer" followed by a space and then the token. 

These tokens only work with the endpoints listed above, they will not work with the standard API used for body-level access. For those, you still need the API keys from the relevant body (or project) pages. 

## **Token expiry** 

As long as an "app_id" is passed (10 characters or more, e.g. an SSID), tokens do not expire, making it possible to use persistent logins with (for example) apps and services. If a token is collected without an "app_id" there is no way for the member to track it reliably, so these tokens will expire within 10 minutes. This is still useful for web services that only need to check a user's credentials as a one-off, but will not allow for persistent calls to the API without a new login every 10 minutes. 

## **Invalidating a token** 

If tokens are suspected to be compromised for whatever reason, there are two ways to invalidate them. The first is to change the private key on the server, this will invalidate _all_ existing tokens and is a quick way to force re-authentication from everybody. Removing the private key from the server config (setting it null or empty) will disable the API - all calls would return a server 500 error due to bad configuration. 

Truncating (or setting all statuses to "revoked") the api_user_token_key table is also an option, and should be done if the above step is taken anyway, as all those keys will be invalid (but will show up as "active" keys until cleared). 

To invalidate a single token, members (or admins) can navigate to the member profile and revoke them one by one. There is currently no way to restore a revoked key (although it would be trivial to flip the status back) - the member will need to log in again on the revoked device and a new key will be created. 

## **Passing parameters to the API** 

Unless otherwise specified, all responses from the auth API (this document) are JSON (header: application/json). Some endpoints will have another content type matching the nature of the request (e.g. profile image, report download, etc.) 

JSON responses can be arrays/objects or in some cases numbers, or string, or one the following three literal names: false null true 

Parameters can be passed either as JSON objects in the request body, or as URL POST/GET parameters. 

## **Example json body** 

```
{ "body_id": 1, "body_type": "group", "permission": "manageMembers" }
```

## **Example alternative using URL parameters** 

```
/api/check_permission?permission=manageMembers&body_type=group&body_id=700
```

## **Endpoints** 

## **Permission check** 

Check whether the authenticated user can perform an action in a body. This follows the same methodology as that used in the system, so access to and knowledge of the roles/permissions system is necessary. 

Common permissions: 

- manageMembers 

- viewReports 

- seeMembers 

Common body types: 

- group, troop, patrol 

- organisation, district, region, corps, network 

- project 

## **Single permission check example** 

```
{"permission": "manageMembers", "body_type": "group", "body_id": 700}
```

```
Or
```

```
/api/check_permission?permission=manageMembers&body_type=group&body_id=700
```

```
Response: true or false
```

## **Multiple permission checks example** 

```
{"permission": ["manageMembers", "viewReports"], "body_type": "group", "body_id": 700}
Or
```

```
/api/check_permission?permission=manageMembers&body_type=group&body_id=700
Response:
```

```
{ "manageMembers": false, "viewReports": true }
```

## **Member list** 

Pass a body type and ID and get a detailed member list. Authenticated member must have "seeMembers" permission. 

E.g. /api/get/memberlist/group/700 

Response contains a "members" array containing the list and all the relevant info, along with two other arrays with term details and contact type details (only the IDs/types are provided in the member list, to save repetition) 

## **Available project/activity list** 

A list of (upcoming) projects that the authenticated member has access to - mirrors the same list on the member profile 

```
/api/get/projects/available
```

## **Registered project/activity list** 

A list of projects that the authenticated member has registered for 

```
/api/get/projects/registered
```

