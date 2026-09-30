# Hacker News Best Stories API

Solution for the Santander coding test. Returns the best n Hacker News stories, sorted by score.

## Run it

dotnet run

Then go to:

http://localhost:5245/swagger

(check the terminal for the actual port)

Or call it directly:

http://localhost:5245/api/stories/best?n=10

## How it works

1. Get the best story IDs from beststories.json
2. Fetch details for each story
3. Sort by score, highest first, return top n

## Assumptions

* beststories.json isn't guaranteed to be sorted by score, so I sort it myself
* Deleted/dead stories are skipped
* Stories with no url (e.g. Ask HN) link to the HN discussion page instead
* A story that fails to load is skipped, not treated as an error

## Avoiding overloading Hacker News

* Results are cached in memory (IDs for 1 min, stories for 2 min)
* Max 20 requests to Hacker News at once

## Given more time

* Unit tests
* Retry failed requests
* Redis if running on more than one server
* Rate limiting on this API
