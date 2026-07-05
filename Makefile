.DEFAULT_GOAL := verify

.PHONY: help build test validate local-up local-down local-reset local-init verify

build:
	./scripts/build.sh

test: build
	./scripts/test.sh

validate: test
	./scripts/validate.sh

pre-commit-validate: validate

local-up:
	docker compose up -d --wait

local-down:
	docker compose down

local-reset:
	docker compose down -v
	rm -rf .local

local-init: local-up
	./scripts/create-local-resources.sh
	./scripts/seed-local-data.sh

verify:
	$(MAKE) local-reset
	$(MAKE) local-init
	$(MAKE) pre-commit-validate
	./scripts/assert-sqs.sh

verify-ci:
	$(MAKE) local-reset
	$(MAKE) local-init
	./scripts/assert-sqs.sh
