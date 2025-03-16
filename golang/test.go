package main

import (
	"fmt"
	"log"
)

func main() {
	X := NewTwitter("5a0be24d859ad6820906bbc28b0ac9219a71489b")
	params := map[string]string{
		"code_challenge":        "challenge",
		"code_challenge_method": "plain",
		"client_id":             "MjdoOFVuNlJwRmxQT1VnWVBiUkw6MTpjaQ",
		"redirect_uri":          "https://dapp.uxlink.io/authGateway",
		"response_type":         "code",
		"scope":                 "tweet.read users.read follows.read follows.write offline.access",
		"state":                 "1867096870644760576",
	}
	authCode, err := X.Oauth2(params)
	if err != nil {
		log.Fatal(err)
	}

	fmt.Println(authCode)

	Oauth1, err := X.Oauth1("6ZWbiQAAAAABuWQYAAABk7oHX3o")
	if err != nil {
		log.Fatal(err)
	}
	fmt.Println(Oauth1)

}
